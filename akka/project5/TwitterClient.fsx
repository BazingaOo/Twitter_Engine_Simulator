#load "references.fsx"
#load "AesEncryption.fsx"
open System
open Akka.Actor
open Akka.FSharp
open FSharp.Json
open WebSocketSharp
open Akka.Configuration
open System.Text
open AesEncryption

// number of user
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : OFF
            loglevel : OFF
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 8555
                    hostname = localhost
                }
            }
        }")
let system = ActorSystem.Create("RemoteFSharp", configuration)
let server = new WebSocket("ws://localhost:8080/websocket")
server.OnOpen.Add(fun args -> System.Console.WriteLine("Open"))
server.OnClose.Add(fun args -> System.Console.WriteLine("Close"))
server.OnMessage.Add(fun args -> System.Console.WriteLine("Msg: {0}", args.Data))
server.OnError.Add(fun args -> System.Console.WriteLine("Error: {0}", args.Message))
server.Connect()

type MessageType = {
    opt : string
    username : string
    content : string
}
let mutable session=""
let aes = new AesEncryption("cbc", 256)
let TwitterClient (mailbox: Actor<string>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        let result = message.Split ','
        let opt = result.[0]
        let username=result.[1]
        if opt="register"then
            let password=result.[2]
            let enc = aes.Encrypt(username,password)
            let nameenc=Encoding.UTF8.GetString enc
            printfn"Encrypt:%s"nameenc
            let serverJson: MessageType = {opt = opt; username = nameenc; content = password;}
            let json = Json.serialize serverJson
            server.Send json
        if opt="login"then
            let password=result.[2]    
            let serverJson: MessageType = {opt = opt; username = username; content = password;}
            let json = Json.serialize serverJson
            server.Send json
            server.OnMessage.Add(fun args -> if args.Data.Contains("Success") then session<-username)
        if opt="send" then
            let tweetcontent=result.[2]
            let serverJson: MessageType = {opt = opt; username = session; content = tweetcontent;}
            let json = Json.serialize serverJson
            server.Send json
        if opt="subscribe" then
            let susername=result.[2]
            let serverJson: MessageType = {opt = opt; username = session; content = susername;}
            let json = Json.serialize serverJson
            server.Send json
        if opt="retweet" then
            let rusername=result.[2]
            let serverJson: MessageType = {opt = opt; username = session; content = rusername;}
            let json = Json.serialize serverJson
            server.Send json    
        if opt="querysubscribe" then
            let serverJson: MessageType = {opt = opt; username = session; content = "";}
            let json = Json.serialize serverJson
            server.Send json
        if opt="query#" then
            let hashTag=result.[2]
            let serverJson: MessageType = {opt = opt; username = session; content = hashTag;}
            let json = Json.serialize serverJson
            server.Send json
        if opt="query@" then
            let mention=result.[2]
            let serverJson: MessageType = {opt = opt; username = session; content = mention;}
            let json = Json.serialize serverJson
            server.Send json
        return! loop()   
    }
    loop ()
let client = spawn system ("User"+(string 1)) TwitterClient 
    
let rec readInput () =
    Console.Write("Enter command: ")
    let input = Console.ReadLine()
    let inpMessage = input.Split ','
    let serverOpt = inpMessage.[0]

    match (serverOpt) with
    | "register" ->
        let username = inpMessage.[1]
        let password = inpMessage.[2]    
        client <! "register,"+username+","+password
        Threading.Thread.Sleep(1000)
        readInput()
    | "login" ->
        let username = inpMessage.[1]
        let password = inpMessage.[2]
        client <! "login,"+username+","+password
        Threading.Thread.Sleep(1000)
        readInput()
    | "send" ->
        let tweetcontent = inpMessage.[1]
        client <! "send,"+session+","+tweetcontent
        Threading.Thread.Sleep(1000)
        readInput()
    | "subscribe" ->
        let susername = inpMessage.[1] 
        client <! "subscribe,"+session+","+susername
        Threading.Thread.Sleep(1000)
        readInput()
    | "retweet" ->
        client <! "retweet,"+session+","+inpMessage.[1]
        Threading.Thread.Sleep(1000)
        readInput()
    | "querysubscribe" ->
        Threading.Thread.Sleep(2000)
        client <! "querysubscribe,"+session
        readInput()
    | "query#" ->
        client <! "query#,"+session+","+inpMessage.[1]
        Threading.Thread.Sleep(2000)
        readInput()
    | "query@" ->
        client <! "query@,"+session+","+inpMessage.[1]
        Threading.Thread.Sleep(2000)
        readInput()
    | "logout" ->
        session<-""
        Threading.Thread.Sleep(1000)
        readInput()
    | _ -> 
        printfn "Invalid Input, Please refer the Report"
        Threading.Thread.Sleep(1000)
        readInput()

readInput()
system.Terminate() |> ignore
0

//a example
//register,tao,123
//register,tao,323//username already exist
//register,haowen,123
//
//login,tao,234 
//login,tao,123 //there will be a session
//
//send,the first message #tag @haowen
//send,asdfasd @123 // error
//send,asdsfa #tag
//send,the second msg @haowen
//
//subscribe,haowen
//subscribe,123
//
//retweet,haowen
//exit,
//login,haowen,123
//send,ahahha #tag
//retweet,tao
//
//querysubscribe,tao
//querysubscribe,haowen
//
//query#,#tag
//query#,#123// error
//
//query@,@haowen
//query@,@tao//error



