open System.ComponentModel.Design
open System.Drawing
open System.Security.Cryptography

#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"

open System
open Akka.FSharp
open Akka.Remote
open Akka.Configuration
open Akka.Serialization
open Akka.Actor

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 9002
                    hostname = localhost
                }
            }
        }")

let system =
    ActorSystem.Create("RemoteFSharp", configuration)
    
    
    
// Tweet class
type Tweet(tweetId: string, tweetContent: string) =
    member this.tweetId = tweetId
    member this.tweetContent = tweetContent
    override this.ToString() = sprintf "tweetContent: %s" this.tweetContent

// User class
type User(username: string, password: string) =
    let mutable subscribeList: User list = List.empty 
    let mutable tweetList: Tweet List = List.empty 
    member this.username = username
    member this.password = password

    member this.addSubscribe value = 
        subscribeList <- List.append subscribeList [ value ]

    member this.getSubscribe() = subscribeList

    member this.addTweet value = 
        tweetList <- List.append tweetList [ value ]

    member this.getTweet() = tweetList
    
    override this.ToString() = sprintf "Username: %s" this.username


//Twitter class
type Twitter() =
    let mutable tweetTable = new Map<string, Tweet>([]) //id tweet
    let mutable userTable = new Map<string, User>([])
    let mutable hashtagTable = new Map<string, Tweet list>([])
    let mutable mentionTable = new Map<string, Tweet list>([])
    member this.getUserTable() = userTable
    member this.getTweetTable() = tweetTable
    member this.register(username, password) =
        let mutable res = ""
        if (userTable.ContainsKey(username)) then
            res <- "username has existed"
        else
            let user = new User(username, password)
            userTable <- userTable.Add(username, user)
            res <- "creat successfully! Username is:  " + username
        res
        
    //login
    member this.login(username,password)=
        let mutable res=""
        if(userTable.ContainsKey(username)) then
            if(password=userTable.Item(username).password)then
                res<- "success,login successfully"
            else
                res<-"error,username or password is not correctness"
        else
            res<- "you do not have a account, plz sign up"
        res
        
    //send
    member this.sendTweet (username,tweetContent) =
        let mutable res = tweetContent
        let time =
            System.DateTime.Now.ToFileTimeUtc() |> string
        let tweet = new Tweet(time, tweetContent)
        let tweetId = username+time
        //Add to personal twitter
        userTable.Item(username).addTweet tweet
        //Add to twitter map
        tweetTable <- tweetTable.Add(tweetId, tweet)
        //Does it contain #
        if (tweetContent.Contains("#")) then
            let findSpace =
                tweetContent.IndexOf(" ", tweetContent.IndexOf("#"))
            let a = tweetContent.IndexOf("#")
            let hashTag =
                tweetContent.Substring(tweetContent.IndexOf("#"), findSpace-a)
            if (hashtagTable.ContainsKey(hashTag) = false) then
                hashtagTable <- hashtagTable.Add(hashTag, (List.empty: Tweet list))
            hashtagTable <- hashtagTable.Add(hashTag, List.append hashtagTable.[hashTag] [ tweet ])
        //Does it contain @
        if (tweetContent.Contains("@")) then
            let findSpace =
                tweetContent.IndexOf(" ", tweetContent.IndexOf("@"))
            let a = tweetContent.IndexOf("@")
            let mention =
                tweetContent.Substring(tweetContent.IndexOf("@"), findSpace-a)
            
            if (mentionTable.ContainsKey(mention) = false) then
                mentionTable <- mentionTable.Add(mention, (List.empty: Tweet list))
            mentionTable <- mentionTable.Add(mention, List.append mentionTable.[mention] [tweet])
        res
   
    // subscribeUser
    member this.subscribeUser (username, sUsername)=
        let mutable res=""
        if(userTable.Item(username).getSubscribe().Equals(userTable.Item(sUsername))) then
            res<- "error! you have subscribed the user!"
        else
            userTable.Item(username).addSubscribe(userTable.Item(sUsername))
            res<- "subscribe success! username:" + sUsername
        res
        
    //reTweet
    member this.reTweet(username,rUsername)=
        let mutable res=""
        if userTable.Item(rUsername).getTweet().Item(0).Equals(null) then
            res<-"this tweet maybe not exist"
        else
            this.sendTweet (username, userTable.Item(rUsername).getTweet().Item(0).tweetContent)|>ignore
            res<- "success reTweet:"+username+" "+rUsername+ (userTable.Item(rUsername).getTweet().Item(0).tweetContent)
        res
        
    //querySubscribe
    member this.querySubscribe(username)=
        let mutable res=""
        if not(userTable.IsEmpty)then
            res<-"querySubscribe success"
            userTable.Item(username).getSubscribe()|>Seq.iter (fun p ->printfn "%A" p)
        else
            res<-"your subscribe list is null, plz subscribe!"
        res
        
    //queryTweetByHashTag
    member this.queryTweetByHashTag(hashTag)=
        let mutable res=""
        if not (hashtagTable.ContainsKey(hashTag))then
            res<-"###there is nothing with "+hashTag
        else
            res<-"###queryTweetByHashTag success"
            hashtagTable.Item(hashTag)|>Seq.iter (fun p ->printfn "%A" p)
        res
        
    //queryTweetByMention
    member this.queryTweetByMention(mUsername)=
        let mutable res=""
        if not (mentionTable.ContainsKey(mUsername))then
            res<-"@@@there is nothing with "+mUsername
        else
            res<-"@@@queryTweetByMention success"
            mentionTable.Item(mUsername)|>Seq.iter (fun p ->printfn "%A" p)
        res 

//Message
type RegisterMessage= RegisterMsg of string*string
type LoginMessage= LoginMsg of string*string
type SendMessage=SendMsg of string*string
type SubscribeMessage=SubscribeMsg of string*string
type RetweetMessage=RetweetMsg of string*string
type QuerySubscribeMessage=QuerySubscribeMsg of string
type QueryHashtagMessage=QueryHashtagMsg of string
type QueryMentionMessage=QueryMentionMsg of string
let twitter = Twitter()
//register actor
let register_actor(mailbox:Actor<_>)=
    let rec loop()=actor{
        let! message =mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | RegisterMsg(username,password)->
            sender<? twitter.register(username,password) |>ignore
        return! loop()
    }
    loop()
//login actor
let login_actor(mailbox:Actor<_>)=
    let rec loop()=actor{
        let! message =mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | LoginMsg(username,password)->
            sender<? twitter.login(username,password) |>ignore
        return! loop()
    }
    loop()
//send actor
let sendTweet_actor(mailbox:Actor<_>)=
    let rec loop()=actor{
        let! message =mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | SendMsg(username,tweetContent)->
            let res= twitter.sendTweet (username, tweetContent)
            sender<? res |>ignore
        return! loop()
    }
    loop()
//subscribe actor
let subscribe_actor(mailbox:Actor<_>)=
    let rec loop()=actor{
        let! message =mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | SubscribeMsg(username,sUsername)->
            sender<? twitter.subscribeUser (username, sUsername) |>ignore
        return! loop()
    }
    loop()

//retweet actor
let retweet_actor(mailbox:Actor<_>)=
    let rec loop()=actor{
        let! message =mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | RetweetMsg(username,rUsername)->
            sender<? twitter.reTweet(username,rUsername) |>ignore
        return! loop()
    }
    loop()
    
//querySubscribe_actor
let querySubscribe_actor(mailbox:Actor<_>)=
    let rec loop()=actor{
        let! message =mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | QuerySubscribeMsg(username)->
            sender<? twitter.querySubscribe(username) |>ignore
        return! loop()
    }
    loop()
    
 //queryHashtag_actor
let queryHashtag_actor(mailbox:Actor<_>)=
    let rec loop()=actor{
        let! message =mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | QueryHashtagMsg(hashTag)->
            sender<? twitter.queryTweetByHashTag(hashTag) |>ignore
        return! loop()
    }
    loop()

//query @
let queryMention_actor(mailbox:Actor<_>)=
    let rec loop()=actor{
        let! message =mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | QueryMentionMsg(mention)->
            sender<? twitter.queryTweetByMention(mention) |>ignore
        return! loop()
    }
    loop()    
let Register_Actor=spawn system "register" register_actor
let Login_Actor=spawn system "login" login_actor
let Send_Actor= spawn system "send" sendTweet_actor
let Subscribe_Actor= spawn system "subscribe" subscribe_actor
let Retweet_Actor=spawn system "retweet" retweet_actor
let QuerySubscribe_Actor=spawn system "querySubscribe" querySubscribe_actor
let QueryHashtag_Actor=spawn system "queryHashtag" queryHashtag_actor
let QueryMention_Actor=spawn system "queryMention" queryMention_actor


let server =
    spawn system "server"
    <| fun mailbox ->
        let rec loop () =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match box message with
                | :? string as message ->
                    printfn "message: %s" message 
                    let mutable res=Register_Actor<?RegisterMsg("","")
                    let result=message.Split(',')
                    let opt=result.[0]
                    let username=result.[1]
                    if opt="register" then //register
                        let password=result.[1]
                        printfn"register：username: %s and password: %s" username password
                        res<-Register_Actor<?RegisterMsg(username,password)
                    if opt="send" then //send
                        let tweetContent=result.[2]
                        printfn"send: tweetContent: %s" tweetContent
                        res<-Send_Actor<?SendMsg(username,tweetContent)
                    if opt="subscribe" then //subscribe
                        let sUsername=result.[2]
                        printfn"subscribe: sUsername: %s" sUsername
                        res<-Subscribe_Actor<?SubscribeMsg(username,sUsername)
                    if opt="retweet" then //retweet
                        let rUsername=result.[2]
                        printfn"retweet: rUsername: %s" rUsername
                        res<-Retweet_Actor<?RetweetMsg(username,rUsername)
                    if opt="querysubscribe" then //querysubscribe    
                         printfn"querysubscribe: username: %s" username
                         res<-QuerySubscribe_Actor<?QuerySubscribeMsg(username)
                    if opt="query#" then
                         let hashTag=result.[2]
                         printfn"query#: hashTag: %s" hashTag
                         res<-QueryHashtag_Actor<?QueryHashtagMsg(hashTag)
                    if opt="query@" then                                              
                         let mention=result.[2]
                         printfn"query@: mention: %s" mention
                         res<-QueryMention_Actor<?QueryMentionMsg(mention)
                    sender <? Async.RunSynchronously (res, 1000)|>ignore
                | _ -> ()
                return! loop()
            }
        loop ()

Console.ReadLine() |> ignore
0





