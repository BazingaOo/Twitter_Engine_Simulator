#load "references.fsx"
#load "AesEncryption.fsx"
open AesEncryption
open System
open Akka.Actor
open Akka.FSharp
open FSharp.Json
open Suave
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.RequestErrors
open Suave.Logging
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Newtonsoft.Json
open Suave.Writers

open System.Security.Cryptography
open System.Text
open System.IO
open System
open System.Text.RegularExpressions

type ResponseType = { Status: string; Data: string }
let system = ActorSystem.Create("TwitterServer")
//a 256-bit ElipticCurve,provides a public key
let aes = new AesEncryption("cbc", 256)

// Tweet class
type Tweet(tweetId: string, tweetContent: string) =
    member this.tweetId = tweetId
    member this.tweetContent = tweetContent

    override this.ToString() =this.tweetContent

// User class
type User(username: string, password: string, webSocket: WebSocket) =
    let mutable subscribeList: User list = List.empty
    let mutable tweetList: Tweet List = List.empty
    let mutable socket = webSocket //websocket
    member this.username = username
    member this.password = password
    member this.addSubscribe value =
        subscribeList <- List.append subscribeList [ value ]
    member this.getSubscribe() = subscribeList
    member this.addTweet value =
        tweetList <- List.append tweetList [ value ]
    member this.getTweet() = tweetList
    member this.GetSocket() = socket
    member this.SetSocket(webSocket) = socket <- webSocket
    override this.ToString() = this.username

//Twitter class
type Twitter() =
    let mutable tweetTable = new Map<string, Tweet>([]) //id tweet
    let mutable userTable = new Map<string, User>([])
    let mutable hashtagTable = new Map<string, Tweet list>([])
    let mutable mentionTable = new Map<string, Tweet list>([])
    member this.getUserTable() = userTable
    member this.getTweetTable() = tweetTable

    member this.register(username, password, webSocket) =
        let mutable res: ResponseType = { Status = ""; Data = "" }
        if (userTable.ContainsKey(username)) then
            res <-
                { Status = "Error"
                  Data = "Register: Username already exists!" }
        else
            let user = new User(username, password, webSocket)
            userTable <- userTable.Add(username, user)
            res <-
                { Status = "Success"
                  Data = "Register: Added successfully!" }
        res

    //login
    member this.login(username, password) =
        let mutable res: ResponseType = { Status = ""; Data = "" }
        if (userTable.ContainsKey(username)) then
            if (password = userTable.Item(username).password) then
                res <-
                    { Status = "Success"
                      Data = "success,login successfully" }
            else
                res <-
                    { Status = "Error"
                      Data ="username or password is not correctness" }
        else
            res <-
                    { Status = "Error"
                      Data ="you do not have a account, plz sign up"}
        res

    //send
    member this.sendTweet(username, tweetContent) =
        let mutable res: ResponseType = { Status = ""; Data = "" }
        let time =System.DateTime.Now.ToFileTimeUtc() |> string
        let tweet = new Tweet(time, tweetContent)
        let tweetId = username + time
        //Add to personal twitter
        userTable.Item(username).addTweet tweet
        //Add to twitter map
        tweetTable <- tweetTable.Add(tweetId, tweet)
        //Does it contain #
        let mutable hashTag1=""
        if (tweetContent.Contains("#")) then
            let findSpace = tweetContent.IndexOf(" ", tweetContent.IndexOf("#"))
            let a = tweetContent.IndexOf("#")
            let hashTag =tweetContent.Substring(tweetContent.IndexOf("#"), findSpace - a)
            hashTag1<-hashTag
            if (hashtagTable.ContainsKey(hashTag) = false) then
                hashtagTable <- hashtagTable.Add(hashTag, (List.empty: Tweet list))
            hashtagTable <- hashtagTable.Add(hashTag, List.append hashtagTable.[hashTag] [ tweet ])
            res <-
                { Status = "success"
                  Data = "Sent:" + tweet.ToString() }
        else
            res <-
                { Status = "success"
                  Data = "Sent:" + tweet.ToString() }
        //Does it contain @
        if (tweetContent.Contains("@")) then
            let findSpace = tweetContent.IndexOf(" ", tweetContent.IndexOf("@"))
            let a = tweetContent.IndexOf("@")
            let mention =tweetContent.Substring(tweetContent.IndexOf("@"), findSpace - a)
            if(userTable.ContainsKey(tweetContent.Substring(tweetContent.IndexOf("@")+1, findSpace - a-1)))then
                if (mentionTable.ContainsKey(mention) = false) then
                    mentionTable <- mentionTable.Add(mention, (List.empty: Tweet list))
                mentionTable <- mentionTable.Add(mention, List.append mentionTable.[mention] [ tweet ])
                res <-
                    { Status = "Success"
                      Data = "Sent:" + tweet.ToString() }
            else
                hashtagTable <- hashtagTable.Remove(hashTag1)
                res <-
                    { Status = "Error"
                      Data = "your mention does not exist" }
        else
            res <-
                { Status = "success"
                  Data = "Sent:" + tweet.ToString() }
        res

    // subscribeUser
    member this.subscribeUser(username, sUsername) =
        let mutable res: ResponseType = { Status = ""; Data = "" }
        if (userTable.ContainsKey(sUsername)=false) then
            res <-{ Status = "Error"
                    Data = sUsername+", this user is not exist!" }
        else
            if (userTable.Item(username).getSubscribe().Equals(userTable.Item(sUsername))) then
                res <-{ Status = "Error"
                        Data = "you have subscribed the user" }
            else
                userTable.Item(username).addSubscribe (userTable.Item(sUsername))
                res <-{ Status = "Success"
                        Data = username + " now is following " + sUsername }
        res

    //reTweet
    member this.reTweet(username, rUsername) =
        let mutable res: ResponseType = { Status = ""; Data = "" }
        if (userTable.Item(rUsername).getTweet().IsEmpty) then
            res <-{ Status = "Error"
                    Data = "this tweet maybe not exist" }
        else
            this.sendTweet (username,userTable.Item(rUsername).getTweet().Item(0).tweetContent)|> ignore
            res <-{ Status = "Success"
                    Data ="reTweet:"+ username+ " "+ rUsername+ (userTable.Item(rUsername).getTweet().Item(0).tweetContent) }

        res

    //querySubscribe
    member this.querySubscribe(username) =
        let mutable res: ResponseType = { Status = ""; Data = "" }

        if not (userTable.Item(username).getSubscribe().IsEmpty) then
            let userList=userTable.Item(username).getSubscribe() |> List.map(fun x->x.ToString()) |> String.concat " "
            //|> Seq.iter (fun p -> printfn "%A" p)
            res <-
                { Status = "Success"
                  Data = userList+" " }
        else
            res <-
                { Status = "Error"
                  Data = "Your subscribe list is null" }

        res

    //queryTweetByHashTag
    member this.queryTweetByHashTag(hashTag) =
        let mutable res: ResponseType = { Status = ""; Data = "" }
        if not (hashtagTable.ContainsKey(hashTag)) then
            res <-
                { Status = "Error"
                  Data = "###there is nothing with" + hashTag }
        else
            let hashtagList = hashtagTable.Item(hashTag)|> List.map(fun x->x.ToString()) |> String.concat " "
            //|> Seq.iter (fun p -> printfn "%A" p)
            res <-
                { Status = "Success"
                  Data = hashtagList+" " }
        res

    //queryTweetByMention
    member this.queryTweetByMention(mUsername) =
        let mutable res: ResponseType = { Status = ""; Data = "" }
        if not (mentionTable.ContainsKey(mUsername)) then
            res <-
                { Status = "Error"
                  Data = "QueryMentions: No mentions are found" }
        else
            let mentionList=mentionTable.Item(mUsername)|> List.map(fun x->x.ToString()) |> String.concat " "
            //|> Seq.iter (fun p -> printfn "%A" p)
            res <-
                { Status = "Success"
                  Data = mentionList+" " }
        res

//Message
type RegisterMessage = RegisterMsg of string * string * WebSocket
type LoginMessage = LoginMsg of string * string
type SendMessage = SendMsg of string * string
type SubscribeMessage = SubscribeMsg of string * string
type RetweetMessage = RetweetMsg of string * string
type QuerySubscribeMessage = QuerySubscribeMsg of string
type QueryHashtagMessage = QueryHashtagMsg of string
type QueryMentionMessage = QueryMentionMsg of string
let twitter = Twitter()
//register actor
let register_actor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | RegisterMsg (username, password, webSocket) ->
                sender<? twitter.register (username, password, webSocket)|> ignore
            return! loop ()
        }
    loop ()
    
//login actor
let login_actor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | LoginMsg (username, password) ->
                sender <? twitter.login (username, password)|> ignore
            return! loop ()
        }

    loop ()
//send actor
let sendTweet_actor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | SendMsg (username, tweetContent) ->
                let res =
                    twitter.sendTweet (username, tweetContent)
                sender <? res |> ignore
            return! loop ()
        }
    loop ()
//subscribe actor
let subscribe_actor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | SubscribeMsg (username, sUsername) ->
                sender<? twitter.subscribeUser (username, sUsername)|> ignore
            return! loop ()
        }
    loop ()
//retweet actor
let retweet_actor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | RetweetMsg (username, rUsername) ->
                sender <? twitter.reTweet (username, rUsername)|> ignore
            return! loop ()
        }

    loop ()
//querySubscribe_actor
let querySubscribe_actor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | QuerySubscribeMsg (username) ->
                sender <? twitter.querySubscribe (username)|> ignore
            return! loop ()
        }

    loop ()

//queryHashtag_actor
let queryHashtag_actor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | QueryHashtagMsg (hashTag) ->
                sender <? twitter.queryTweetByHashTag (hashTag)|> ignore
            return! loop ()
        }
    loop ()
//query @
let queryMention_actor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | QueryMentionMsg (mention) ->
                sender <? twitter.queryTweetByMention (mention)
                |> ignore
            return! loop ()
        }

    loop ()


let Register_Actor = spawn system "register" register_actor
let Login_Actor = spawn system "login" login_actor
let Send_Actor = spawn system "send" sendTweet_actor
let Subscribe_Actor = spawn system "subscribe" subscribe_actor
let Retweet_Actor = spawn system "retweet" retweet_actor
let QuerySubscribe_Actor =spawn system "querySubscribe" querySubscribe_actor
let QueryHashtag_Actor =spawn system "queryHashtag" queryHashtag_actor
let QueryMention_Actor =spawn system "queryMention" queryMention_actor

type MessageType =
    { opt: string
      username: string
      content: string }

type ApiActorOp = SendOpt of MessageType * WebSocket

let Server (mailbox: Actor<ApiActorOp>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match message with
            | SendOpt (message, webSocket) ->
                //printfn "webSocket: %A" webSocket.ToString
                let opt = message.opt
                let mutable res = Register_Actor <? RegisterMsg("", "", webSocket)
                if opt = "register" then //register
                    let password = message.content
                     //Decryption
                    let encname=Encoding.UTF8.GetBytes message.username
                    let username=Encoding.UTF8.GetString (aes.Decrypt(encname,message.content))
                    printfn"After Decrypt,the username is:%s"username
                    res <-Register_Actor<? RegisterMsg(username, password, webSocket)
                    
                let username = message.username
                if opt = "login" then //login
                    let password = message.content
                    res <-Login_Actor<? LoginMsg(username, password)
                if opt = "send" then //send
                    let tweetContent = message.content
                    res <- Send_Actor <? SendMsg(username, tweetContent)
                if opt = "subscribe" then //subscribe
                    let sUsername = message.content
                    res <-Subscribe_Actor<? SubscribeMsg(username, sUsername)
                if opt = "retweet" then //retweet
                    let rUsername = message.content
                    res <- Retweet_Actor <? RetweetMsg(username, rUsername)
                if opt = "querysubscribe" then //querysubscribe
                    res <-QuerySubscribe_Actor<? QuerySubscribeMsg(username)
                if opt = "query#" then
                    let hashTag = message.content
                    res <- QueryHashtag_Actor <? QueryHashtagMsg(hashTag)
                if opt = "query@" then
                    let mention = message.content
                    res <- QueryMention_Actor <? QueryMentionMsg(mention)
                sender <? Async.RunSynchronously(res, 1000)
                |> ignore

            return! loop ()
        }

    loop ()

let server = spawn system "Server" Server
server <? "" |> ignore
printfn "*****************************************************"
printfn "************Starting Twitter Server!!************"
printfn "*****************************************************"


//WebSocket    
let ws (webSocket: WebSocket) (context: HttpContext) =
    socket {
        // if `loop` is set to false, the server will stop receiving messages
        let mutable loop = true
        while loop do
            // the server will wait for a message to be received without blocking the thread
            let! msg = webSocket.read ()
            match msg with
            | (Text, data, true) ->
             
                let str = UTF8.toString data
                let mutable json = Json.deserialize<MessageType> str
                printfn"Deserialized Json:"
                printfn"%A"json
                let mutable task = server <? SendOpt(json, webSocket)
                let response: ResponseType = Async.RunSynchronously(task, 10000)
                let byteResponse =
                    Json.serialize response
                    |> System.Text.Encoding.ASCII.GetBytes
                    |> ByteSegment
                do! webSocket.send Text byteResponse true

            | (Close, _, _) ->
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true
                loop <- false

            | _ -> ()
}



let app: WebPart =
    choose [ path "/websocket" >=> handShake ws
             NOT_FOUND "Found no handlers." ]

startWebServer
    { defaultConfig with
          logger = Targets.create Verbose [||] }
    app

0
