module Plexus.Types

open System
open Helpers
open System.Net
open System.Net.Sockets
open System.IO
open System.Collections.Concurrent

let newid = 
    let s = Seq.initInfinite(fun i -> int64 i).GetEnumerator()
    fun() -> 
        if s.MoveNext() then
            s.Current
        else
            s.Reset()
            s.Current


type KeepAlive<'a> = 
    | Ping of 'a
    | Pong of 'a
    with 
        member x.ToByteArray() =
            serialize x
        static member FromByteArray(buffer:byte[]) = 
            deserializeAs<KeepAlive<'a>> buffer

type Message<'a> = 
    { Path:string; Payload:'a; ReturnPath:string }
    with 
        member x.ToByteArray() = 
            serialize x
        static member FromByteArray<'a>(buffer:byte[]) =
            deserializeAs<Message<'a>> buffer

[<CLIMutable>]
type Response<'a> = 
    { Path:string; Response:'a }
    with
        member x.ToByteArray() = 
            serialize x
        static member FromByteArray(buffer:byte[]) = 
            deserializeAs<Response<'a>> buffer

type internal Packet<'a> =
    | Empty
    | K of KeepAlive<'a>
    | M of Message<'a>
    | R of Response<'a>
    with 
        member x.ToByteArray() = 
            serialize x

let internal pingPacket (id:int64):Packet<int64> = K(Ping(id))
let internal pongPacket (id:int64):Packet<int64> = K(Pong(id))

type RemoteInfo = { Address:IPAddress; Socket:Socket;  LastUsed:DateTime }

type Socket with
    member x.CompletedSynchronously(arg) =
        let event = new Event<SocketAsyncEventArgs>()
        event.Trigger(arg)
        Async.AwaitEvent(event.Publish)

    member x.AsyncConnect(a:SocketAsyncEventArgs) =
        let async = Async.AwaitEvent(a.Completed)
        if x.ConnectAsync(a) then
            async
        else
            x.CompletedSynchronously(a)

    member x.AsycAccept (a:SocketAsyncEventArgs)=
        let async = Async.AwaitEvent(a.Completed)
        if x.AcceptAsync(a) then
           async
        else
            x.CompletedSynchronously(a)

    member x.AsyncReceive (a:SocketAsyncEventArgs) =
        let async = Async.AwaitEvent(a.Completed)
        if x.ReceiveAsync(a) then
            async
        else
            x.CompletedSynchronously(a)

    member x.AsyncSend(a:SocketAsyncEventArgs) =
        let async = Async.AwaitEvent(a.Completed)
        if x.SendAsync(a) then
            async
        else
            x.CompletedSynchronously(a)

    member x.AsyncDisconnect(a:SocketAsyncEventArgs) = 
        let async = Async.AwaitEvent(a.Completed)
        if x.DisconnectAsync(a) then
            async
        else
            x.CompletedSynchronously(a)

    member x.GetRemoteAddress() = 
        (x.RemoteEndPoint :?> IPEndPoint).Address

type BufferManager(totalBytes:int, bufferSize:int) = 
    let mutable currentIndex = 0
    let mutable freeIndexPool = List.empty<int>
    let buffer = Array.zeroCreate<byte>(totalBytes)

    member x.SetBuffer(arg:SocketAsyncEventArgs) = 
        match freeIndexPool.Length with
        | 0 -> 
            if (totalBytes - bufferSize) < currentIndex then
                false
            else
                arg.SetBuffer(buffer,currentIndex,bufferSize)
                currentIndex <- currentIndex + bufferSize
                true
        | _ ->
            let index = freeIndexPool |> List.head
            freeIndexPool <- freeIndexPool |> List.tail
            arg.SetBuffer(buffer,index,bufferSize)
            true

    member x.FreeBuffer(arg:SocketAsyncEventArgs) =
        freeIndexPool <- arg.Offset :: freeIndexPool
        arg.SetBuffer(null,0,0)

type SocketAsyncEventArgsPool(capacity) = 
    let mutable disposed = false
    let pool = new BlockingCollection<SocketAsyncEventArgs>()
    
    member x.Init = 
        let rec loop n = 
            match n with 
            | 0 -> ignore()
            | _ ->
                pool.Add(new SocketAsyncEventArgs())
                loop (n - 1)
        loop capacity            

    member x.Push(item:SocketAsyncEventArgs) =
        match item with
        | null -> failwith "Items added to a SocketAsyncEventArgsPool cannot be null"
        | _ -> pool.Add(item)

    member x.Pop() = 
        pool.Take()

    interface IDisposable with
        member this.Dispose() = 
            if not disposed then
                disposed <- true
                pool.CompleteAdding()
                while pool.Count > 1 do
                    (pool.Take() :> IDisposable).Dispose()
                pool.Dispose()
