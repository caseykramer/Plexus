module Plexus.Client

open Helpers
open Types
open System
open System.Net
open System.Net.Sockets
open System.Timers


type Client(receiveBufferSize:int, timeout:TimeSpan) as this = 
    let sendReceivePool = new SocketAsyncEventArgsPool(5)
    let bufferPool = new BufferManager(5 * receiveBufferSize,receiveBufferSize) // Magic Numbers....I love em!
    let socket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
    let timer = new Timer(timeout.TotalMilliseconds / 4.0, AutoReset = true, Enabled = true)
    let mutable disposed = false
    let mutable connectedEventArgs:SocketAsyncEventArgs = null

    let received = new Event<Packet<_>>()

    let rec doSend buffer = 
        async {
            let sendArgs = sendReceivePool.Pop()
            sendArgs.SetBuffer(buffer,0,buffer.Length)
            let! arg = socket.AsyncSend(sendArgs)
            match arg.SocketError with
            | SocketError.Success -> sendReceivePool.Push(arg)
            | _ -> failwith "Socket Error: %A" arg.SocketError
        }

    let rec doReceive arg = 
        async{
            bufferPool.SetBuffer(arg) |> ignore
            let! receive = socket.AsyncReceive(arg)
            match receive.SocketError with
            | SocketError.Success ->
                do! processReceive receive.Buffer.[receive.Offset..(receive.Offset + receive.BytesTransferred - 1)]
                bufferPool.FreeBuffer(arg)
                return! doReceive arg
            | _ -> failwith (sprintf "Socket Error: %A" receive.SocketError)
            
        }
    and processReceive buffer = 
        async {
            match tryDeserialize<Packet<_>> buffer with
            | Some(p) -> 
                match p with
                | K(Ping(id)) ->
                    do! doSend (K(Pong(id)).ToByteArray())
                | _ -> received.Trigger(p)
            | _ -> failwith "Unknown response received"
        } 

    let doConnect arg = 
        async {            
            let! connected = socket.AsyncConnect(arg)
            match connected.SocketError with
            | SocketError.Success -> 
                connectedEventArgs <- connected 
                do! doReceive arg
            | _ -> failwith "Socket Error: %A" connected.SocketError
        }

    let doDisconnect arg = 
        async {
            let! disconnected = socket.AsyncDisconnect(arg)
            match disconnected.SocketError with
            | SocketError.Success -> ignore()
            | _ -> failwith "Socket Error: %A" disconnected.SocketError
        }

    do
        sendReceivePool.Init
        timer.Elapsed.Add(fun e -> this.Ping())

    
    member x.Connect(hostname,port) = 
        let address = 
            match Dns.GetHostAddresses(hostname) |> Array.tryFind(fun a -> a.AddressFamily = AddressFamily.InterNetwork) with
            | Some(addr) -> addr
            | None -> failwith (sprintf "No Ipv4 address available for hostname %s" hostname)
        let arg = sendReceivePool.Pop()
        arg.RemoteEndPoint <- new IPEndPoint(address,port)
        doConnect arg |> Async.Start
        timer.Start()

    member x.Ping() =
        async {
            let id = newid()
            let packet = pingPacket id
            do! doSend <| packet.ToByteArray()
            let! result = Async.AwaitEvent(received.Publish)
            return 
                match result with
                | K(Pong(g)) when g = id -> true
                | _ -> false
        } |> Async.RunSynchronously |> ignore

    member x.Send(buffer:byte[]) = 
        doSend buffer |> Async.Start

    member x.Disconnect() = 
        doDisconnect (sendReceivePool.Pop()) |> Async.Start

    interface IDisposable with
        member x.Dispose() = 
            disposed <- true
            (sendReceivePool :> IDisposable).Dispose()
            