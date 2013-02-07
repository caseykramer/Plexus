module Plexus.Helpers

    open System
    open System.IO
    open System.Collections.Generic

    let memoize<'a,'b when 'a :comparison> f =
        let cache = ref Map.empty<'a,'b>
        fun x ->
            match (!cache).TryFind(x) with
            | Some res -> res
            | None ->
                 let res = f x
                 cache := (!cache).Add(x,res)
                 res

    let asAction<'a> f  = new System.Action<'a>(f)

    
    let deserialize<'a> (buffer:byte array) = 
        let packer = MsgPack.ObjectPacker()
        packer.Unpack(buffer)    
    
    let deserializeAs<'a> (buffer:byte array) = 
        let packer = new MsgPack.ObjectPacker()
        packer.Unpack<'a>(buffer)

    let serialize target:byte[] =
        let packer = new MsgPack.ObjectPacker()
        packer.Pack(target)

    let tryDeserialize<'a> buffer = 
        try
            Some(deserializeAs<'a> buffer)
        with
            | :? Exception as ex-> 
                ex |> Log.errorEx "Deserialization error"
                None
