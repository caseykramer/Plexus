module Plexus.BSON 

    let serialize item:byte array = 
        Array.empty<byte>

    let deserialize bytes:'a = 
        Unchecked.defaultof<'a>

