// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open Actors
open System
open Plexus

type Message1 = { Timestamp:DateTime; Message:string }
type Message2 = { Timestamp:DateTime; Shutdown:bool }
    
let actor1 = 
    actor 
        (fun msg -> 
            printfn "%A" msg)

let actor2 = 
    actor 
        (fun msg -> 
            match msg.Shutdown with
            | false -> ignore()
            | true -> Environment.Exit 0)

[<EntryPoint>]
let main argv = 
    printfn "Actors demo app"
    printfn "Two actors are initialized, one echos messages to the consle, the other exists" 
    printfn "At the prompt enter any text to see it echoed back with some addition metadata for fun."
    printfn "Enter \"exit\" on a line by itself to quit"
    let rec readLoop i:int =
        printf "actors > "
        let input = Console.ReadLine()
        match input with
        | "" -> readLoop i
        | "exit" -> 
            let msg = { Timestamp = DateTime.Now; Shutdown = true }
            actor2 => msg
            readLoop i
        | _ ->
            let msg = { Timestamp = DateTime.Now; Message = input }
            actor1 => msg
            readLoop i
    readLoop 0 |> ignore
    0
