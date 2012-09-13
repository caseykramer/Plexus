module Plexus.Log
    open System
    open log4net

    type private LogInfo = { Message:string; Exception:Exception option }
    let private noErrorInfo = { Message = ""; Exception= None }

    type private LogMessage = 
        | Debug of LogInfo
        | Info of LogInfo
        | Warn of LogInfo
        | Error of LogInfo
        | Fatal of LogInfo

    let private l4nLogger = LogManager.GetLogger("RemoteActors")

    let private logger = MailboxProcessor<LogMessage>.Start(fun inbox ->
        let logWith info f fExcep = 
            match info.Exception with
            | None -> f info.Message
            | Some(ex) -> fExcep info.Message ex
        let rec inboxLoop =
            async {
                let! msg = inbox.Receive()
                match msg with
                | Debug(info) -> l4nLogger.Debug(info.Message)
                | Info(info) -> l4nLogger.Info(info.Message)
                | Warn(info) -> logWith info (fun m -> l4nLogger.Warn(m)) (fun m -> fun e -> l4nLogger.Warn(m,e))
                | Error(info) -> logWith info (fun m -> l4nLogger.Error(m)) (fun m -> fun e -> l4nLogger.Error(m,e))
                | Fatal(info) -> logWith info (fun m -> l4nLogger.Fatal(m)) (fun m -> fun e -> l4nLogger.Fatal(m,e))
                return! inboxLoop
            }
        inboxLoop)

    let debug message = 
        let msg = { noErrorInfo with Message = message }
        logger.Post(Debug(msg))

    let info message = 
        let msg = { noErrorInfo with Message = message }
        logger.Post(Info(msg))

    let warn message = 
        let msg = { noErrorInfo with Message = message }
        logger.Post(Warn(msg))

    let warnEx message excep = 
        let msg = { Message = message; Exception = Some(excep) }
        logger.Post(Warn(msg))

    let error message = 
        let msg = { noErrorInfo with Message = message }
        logger.Post(Error(msg))

    let errorEx message excep =
        let msg = { Message = message; Exception = Some(excep) }
        logger.Post(Error(msg))
