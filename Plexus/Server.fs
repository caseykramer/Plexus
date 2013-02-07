module Plexus.Server

open System
open System.Net
open System.IO
open System.Timers
open System.Net.Sockets
open System.Collections.Concurrent
open Helpers
open Types         


type Server(numConnections:int,receiveBufferSize:int,timeout:TimeSpan) = 
    let opsToPreAlloc = 2
    let maxErrors = 5
    let timer = new Timer(timeout.TotalMilliseconds / 4.0, AutoReset = true, Enabled = true)
    let bufferManager = new BufferManager(receiveBufferSize * numConnections * opsToPreAlloc, receiveBufferSize)
    let receivePool = new SocketAsyncEventArgsPool(numConnections)
    let activeSockets = new ConcurrentDictionary<IPAddress,RemoteInfo>()
    let socketErrors = new ConcurrentDictionary<IPAddress,int>()
    let received = new Event<Packet<_>>()
    let mutable listeningSocket:Socket = null

    let rec doSend buffer (socket:Socket) = 
        async {
            let sendArgs = receivePool.Pop()
            sendArgs.SetBuffer(buffer,0,buffer.Length)
            let! arg = socket.AsyncSend(sendArgs)
            match arg.SocketError with
            | SocketError.Success -> receivePool.Push(arg)
            | _ -> failwith "Socket Error: %A" arg.SocketError
        }

    let rec doReceive arg (socket:Socket) = 
        async{
            bufferManager.SetBuffer(arg) |> ignore
            let! receive = socket.AsyncReceive(arg)
            match receive.SocketError with
            | SocketError.Success ->
                do! socket |> processReceive receive.Buffer.[receive.Offset..(receive.Offset + receive.BytesTransferred - 1)]
                bufferManager.FreeBuffer(arg)
                return! doReceive arg socket
            | _ -> failwith (sprintf "Socket Error: %A" receive.SocketError)            
        }
    and processReceive buffer socket = 
        async {
            let address = socket.GetRemoteAddress()
            match tryDeserialize<Packet<_>> buffer with
            | Some(p) -> 
                match p with
                | K(Ping(id)) ->
                    sprintf "Pong %A" address |> Log.debug
                    do! socket |> doSend (K(Pong(id)).ToByteArray())
                | _ ->
                    sprintf "Received: %A from %A" p address |> Log.debug 
                    received.Trigger(p)
            | _ ->
                sprintf "Unknown response received from %A" address |> Log.warn 
                //failwith "Unknown response received"
        }

    let rec attemptConnect() = 
        let arg = receivePool.Pop()
        bufferManager.SetBuffer(arg) |> ignore
        async {
            let! accepted = listeningSocket.AsycAccept arg
            attemptConnect()
            match accepted.LastOperation with
            | SocketAsyncOperation.Accept ->
                let acceptSocket = accepted.AcceptSocket
                activeSockets.TryAdd((acceptSocket.GetRemoteAddress()),{ Address=acceptSocket.GetRemoteAddress(); Socket=acceptSocket; LastUsed = DateTime.UtcNow}) |> ignore
                do! doReceive arg acceptSocket 
            | _ -> ignore()
        } |> Async.Start

    let findSocketFor =
        memoize(fun name ->
            let a = Dns.GetHostAddresses(name) |> Array.tryFind(fun a -> a.AddressFamily = AddressFamily.InterNetwork)
            match a with
            | None -> failwith <| sprintf "Cannot find IPv4 address for host %A" name
            | Some(address) ->
                let remote = ref Unchecked.defaultof<RemoteInfo>
                match activeSockets.TryGetValue(address, remote) with
                | false -> failwith <| sprintf "Not connected to host: %A" name
                | _ -> (!remote).Socket)

    let updateTimestamp (address:IPAddress) = 
        let socketInfo = ref { Address = address; Socket = null; LastUsed = DateTime.MinValue }
        match activeSockets.TryGetValue(address,socketInfo) with
        | true -> activeSockets.TryUpdate(address,{!socketInfo with LastUsed = DateTime.UtcNow },!socketInfo) |> ignore
        | false -> ignore()

    let incrError (address:IPAddress) = 
        sprintf "Error occured for address %A" address |> Log.warn
        let errors = socketErrors.GetOrAdd(address,fun ip -> 0)
        socketErrors.TryUpdate(address,errors + 1,errors) |> ignore


    let doPing (socket:Socket) =
        sprintf "Ping %A" (socket.GetRemoteAddress()) |> Log.debug
        async {
            let id = newid()
            let message = K(Ping(id)).ToByteArray()
            do! doSend message socket
            let! pong = Async.AwaitEvent(received.Publish)
            match pong with
            | K(Pong(id)) ->
                updateTimestamp <| socket.GetRemoteAddress()
                ignore()
            | _ -> incrError <| socket.GetRemoteAddress()
        }
        
    let removeErrored() =
        let remote = ref Unchecked.defaultof<RemoteInfo>
        let count = ref Unchecked.defaultof<int>
        let rec removeError addresses = 
            match addresses with
            | [] -> ignore()
            | address::others ->
                match activeSockets.TryRemove(address,remote) with
                | true -> socketErrors.TryRemove(address,count) |> ignore
                | _ -> incrError address
                removeError others
        socketErrors |> List.ofSeq 
                     |> List.filter (fun e -> e.Value >= maxErrors) 
                     |> List.map (fun e -> e.Key) 
                     |> removeError
    do
        receivePool.Init
        timer.Elapsed.Add(fun e -> removeErrored())

    member x.Start(endpoint:IPEndPoint) =
        listeningSocket <- new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        listeningSocket.Bind(endpoint)
        listeningSocket.Listen(numConnections)
        attemptConnect()
        timer.Start()
        
    member x.Ping(host) =
        let socket = findSocketFor host
        doPing socket |> Async.RunSynchronously

