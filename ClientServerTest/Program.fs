// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open Plexus
open System.Net

type Message = 
    | Ping
    | Text of string

[<EntryPoint>]
let main argv =
    log4net.Config.BasicConfigurator.Configure(new log4net.Appender.ConsoleAppender())
    let server = new Plexus.Server.Server(20,3000,System.TimeSpan.FromMinutes(5.0))
    let serverPort = 20002
    let address = Dns.GetHostAddresses("127.0.0.1") |> Array.find(fun a -> a.AddressFamily = Sockets.AddressFamily.InterNetwork)
    printfn "starting server on port %i" serverPort
    server.Start(new IPEndPoint(address,serverPort))
    printfn "starting client"
    let client = new Plexus.Client.Client(3000,System.TimeSpan.FromSeconds(30.0))
    client.Connect("localhost",serverPort)
    let rec doLoop() =
        let read = System.Console.ReadLine()
        match read.ToLowerInvariant() with
        | "ping" -> client.Ping()
        | "exit" -> ignore();
        | _ -> client.Send(Plexus.Helpers.serialize <| Text(read))
        doLoop()
    doLoop()
    0
