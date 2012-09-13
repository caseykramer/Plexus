namespace Actors

open System
namespace Plexus

open System


[<AbstractClass>]
type Actor<'a>() as this =
    let received = new Event<'a>()
    let mailbox = MailboxProcessor.Start(fun inbox ->
        let rec inboxLoop =
            async {
                let! msg = inbox.Receive()
                received.Trigger(msg)
                return! inboxLoop 
            }
        inboxLoop)

    abstract member Handle:'a->unit
    abstract member Handle:'a*Action<obj>->unit

    member x.OnError(handler:System.Action<System.Exception>) = mailbox.Error.Add(fun exn -> handler.Invoke(exn))
    member x.Post(msg:'a) = mailbox.Post(msg)
    member x.PostWithReply(msg:'a) = mailbox.PostAndAsyncReply(fun asyncReply -> msg) |> Async.StartAsTask

[<AutoOpen>]
module Actors = 
    let (=>) (actor:Actor<'a>) (message:'a) =
        actor.Post(message)
    
    type private FunctionalActor<'a>(handle,withReply) =
        inherit Actor<'a>() with
            override x.Handle(value) = handle(value)
            override x.Handle(value:'a,action:Action<obj>) = action.Invoke(withReply value)

    let actor handle = 
        new FunctionalActor<'a>(handle,(fun v ->
                                            handle(v)
                                            null)) :> Actor<'a>

    let replyingActor<'a> handle = 
        new FunctionalActor<'a>((fun v -> handle(v) |> ignore),handle) :> Actor<'a>
        
