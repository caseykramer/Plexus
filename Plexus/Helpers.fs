module Plexus.Helpers

    open System
    open ProtoBuf
    open System.IO
    open System.Collections.Generic


    let asAction<'a> f  = new System.Action<'a>(f)

    let deserialize buffer = 
        use stream = new MemoryStream(buffer,false)
        Serializer.Deserialize(stream)

    let tryDeserialize<'a> buffer = 
        use stream = new MemoryStream(buffer,false)
        try
            Some(Serializer.Deserialize<'a>(stream))
        with
            | :? Exception -> None
    
    let deserializeAs<'a> buffer = 
        use stream = new MemoryStream(buffer,false)
        Serializer.Deserialize<'a>(stream)

    let serialize (target:'a):byte[] =
        let buffer = Array.empty<byte>
        use stream = new MemoryStream(buffer,true)
        Serializer.Serialize<'a>(stream,target)
        buffer

    type FirstBuilder() = 
        member this.Bind(x,f) = 
            match x with 
            | Some(x) -> x
            | None -> f(x)
        member this.Return(x) = Some(x)
        member this.Delay(x) = x()
        member this.Zero() = None

    let first = new FirstBuilder()

    let memoize<'a,'b when 'a :comparison> f =
        let cache = ref Map.empty<'a,'b>
        fun x ->
            match (!cache).TryFind(x) with
            | Some res -> res
            | None ->
                 let res = f x
                 cache := (!cache).Add(x,res)
                 res