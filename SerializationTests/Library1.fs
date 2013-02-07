namespace SerializationTests

open ProtoBuf
open NUnit.Framework
open FsUnit

type SimpleUnion =
    | Empty

[<TestFixture>]
type ``Protobuf-net serialization tests`` () = 

    [<Test>]
    member test.
     ``Serialize and Deserialize single-element UNION type``()=
            let stream= new System.IO.MemoryStream()
            let serialized = ProtoBuf.Serializer.Serialize<SimpleUnion>(stream,Empty)
            let deserialized = ProtoBuf.Serializer.Deserialize<SimpleUnion>(stream)
            deserialized |> should be (sameAs Empty)
            let typeModel = ProtoBuf.Meta.TypeModel.Create()
            typeModel.Add(typedefof<SimpleUnion>,true)
