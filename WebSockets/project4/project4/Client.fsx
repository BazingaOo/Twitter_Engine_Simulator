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
open System.Threading
let N = fsi.CommandLineArgs.[1] |> int //number of user
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
                 port = 2552
                 hostname = localhost
             }
         }
     }")
let system = ActorSystem.Create("RemoteFSharp", configuration)
let server = system.ActorSelection("akka.tcp://RemoteFSharp@localhost:9002/user/server")

let random=System.Random(1)
let client_simulator (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        let randomId = message
        match box message with
        | :? string ->
            let mutable random_num=Random().Next()%7
            let mutable user="user"+(string randomId)
            let mutable tUsername="user"+string (random.Next(N)) //目标用户
            let mutable hashTag="#topic"+ random.Next(N).ToString()
            let mutable mention="@user"+random.Next(N).ToString()
            let mutable ref=""
            if random_num=0 then //Register
                let mutable password="password"+(string randomId)
                ref<-"register"+","+user+","+password
            if random_num=1 then //Send
                let mutable tweetContent="this is random tweet"+random.Next(N).ToString()+" "+hashTag+" "+mention+" "
                ref<-"send"+","+user+","+tweetContent
            if random_num=2 then //Subscribe
                ref<-"subscribe"+","+user+","+tUsername
            if random_num=3 then  //Retweet
                ref<-"retweet,"+user+","+tUsername
            if random_num=4 then //QuerySubscribe
                ref<-"querysubscribe"+","+user 
            if random_num=5 then //HashTag
                ref<- "query#"+","+user+","+hashTag
            if random_num=6 then //Mention
                ref<- "query@"+","+user+","+mention
            printfn "[Client] %s" ref
            let response =Async.RunSynchronously (server<? ref, 3000)
            printfn "[Server] %s" (string(response))
            //server<? ref|>ignore
        | _ -> ()
        return! loop()
    }
    loop ()
let Client_Simulator = spawn system "client_simulator" client_simulator   
    


printfn "************************************************************"
printfn "*****************    Register   ****************************" 
printfn "************************************************************"
let Register = System.Diagnostics.Stopwatch.StartNew()
for i in 0..N-1 do
    let cmd ="register,user"+(string i)+",password"+(string i)
    let response = Async.RunSynchronously (server <? cmd, 3000)
    printfn "[Client] %s" cmd
    printfn "[Server] %s" (string(response))
    
Register.Stop()
let time_register = Register.Elapsed.TotalMilliseconds
printfn""
printfn""

    

printfn "************************************************************"
printfn "*****************    Send Tweet   ****************************" 
printfn "************************************************************"
let mutable SendCount=0
let send = System.Diagnostics.Stopwatch.StartNew()
for i in 0..N-1 do
     let rate =random.Next(10)
     if (rate = 5)then
        printfn "disconnectd"  
     else
        SendCount<-SendCount+1
        for j in 0..1 do
            let cmd = "send,user"+(string i)+",I am user"+(string i)+" This is"+(string j)+"th tweet @user"+(string (random.Next(N)))+" #topic"+(string (random.Next(N)))+" "
            let response = Async.RunSynchronously (server <? cmd, 3000)
            printfn "[Client] %s" cmd
            printfn "[Server] %s" (string(response)) 
send.Stop()
let time_send = send.Elapsed.TotalMilliseconds
printfn""
printfn""



printfn "************************************************************"
printfn "*****************    subscribe   ****************************" 
printfn "************************************************************"
let mutable step = 1
let subscribe = System.Diagnostics.Stopwatch.StartNew()
for i in 0..N-1 do
    for j in 0..step..N-1 do
        if not (j=i) then
            let cmd = "subscribe,user"+(string j)+",user"+(string i)
            let response = Async.RunSynchronously (server <? cmd, 3000)
            printfn "[Client] %s" cmd
            printfn "[Server] %s" (string(response))
        step <- step+1
subscribe.Stop()
let time_zipf_subscribe = subscribe.Elapsed.TotalMilliseconds        
printfn""
printfn""

printfn "************************************************************"
printfn "*****************    Retweet   ****************************" 
printfn "************************************************************"
let mutable retweetCount=0
let retweet = System.Diagnostics.Stopwatch.StartNew()
for i in 0..N-1 do
     let rate =random.Next(10)
     if (rate = 5)then
         printfn "disconnectd"  
     else
         retweetCount<-retweetCount+1
         for j in 0..1 do
            let cmd = "retweet,user"+(string i)+",user"+(string j)
            let response = Async.RunSynchronously (server <? cmd, 3000)
            printfn "[Client] %s" cmd
            printfn "[Server] %s" (string(response))
retweet.Stop()
let time_retweet = retweet.Elapsed.TotalMilliseconds        
printfn""
printfn""


printfn "************************************************************"
printfn "*****************    Querysubscribe   ****************************" 
printfn "************************************************************"
let mutable qsbCount=0
let querysubscribe = System.Diagnostics.Stopwatch.StartNew()
for i in 0..N-1 do
      let rate =random.Next(10)
      if (rate = 5)then
         printfn "disconnectd"  
      else
        qsbCount<-qsbCount+1
        let cmd = "querysubscribe,user"+(string i)
        let response = Async.RunSynchronously (server <? cmd, 3000)
        printfn "[Client] %s" cmd
        printfn "[Server] %s" (string(response))
querysubscribe.Stop()
let time_querysubscribe = querysubscribe.Elapsed.TotalMilliseconds   
printfn""
printfn""
 
 
 
printfn "************************************************************"
printfn "*****************    QueryHashTag   ****************************" 
printfn "************************************************************"
let mutable hashtagCount=0
let queryHashtag = System.Diagnostics.Stopwatch.StartNew()
for i in 0..N-1 do
      let rate =random.Next(10)
      if (rate = 5)then
         printfn "disconnectd"  
      else
        hashtagCount<-hashtagCount+1
        let cmd = "query#,user"+(string i)+",#topic"+(string (random.Next(N)))
        let response = Async.RunSynchronously (server <? cmd, 3000)
        printfn "[Client] %s" cmd
        printfn "[Server] %s" (string(response))
queryHashtag.Stop()
let time_queryHashtag = queryHashtag.Elapsed.TotalMilliseconds
printfn""
printfn""


printfn "************************************************************"
printfn "*****************    QueryMention   ****************************" 
printfn "************************************************************"
let mutable mentionCount=0
let queryMention = System.Diagnostics.Stopwatch.StartNew()
for i in 0..N-1 do
     let rate =random.Next(10)
     if (rate = 5)then
         printfn "disconnectd"  
     else
        mentionCount<-mentionCount+1
        let cmd = "query@,user"+(string i)+",@user"+(string (random.Next(N)))
        let response = Async.RunSynchronously (server <? cmd, 3000)
        printfn "[Client] %s" cmd
        printfn "[Server] %s" (string(response))
queryMention.Stop()
let time_queryMention = queryMention.Elapsed.TotalMilliseconds
printfn""
printfn""



printfn "************************************************************"
printfn "*****************    Random   ****************************" 
printfn "************************************************************"
let mutable randomCount=0
let Random = System.Diagnostics.Stopwatch.StartNew()
for i in 0..N-1 do
     let rate =random.Next(10)
     if (rate <> 5)then
        randomCount<-randomCount+1
        Client_Simulator<! string (random.Next(N))
        Thread.Sleep(50)
Random.Stop()
let time_random = Random.Elapsed.TotalMilliseconds
printfn""
printfn""



printfn"************************************************************"
printfn"********************   performance   ***********************"
printfn "register %d users, time is %f" N time_register
printfn "%d users send 2 tweets of per user, time is %f and live connection user is %i" N time_send SendCount
printfn "Zipf subscribe %d users, time is %f" N time_zipf_subscribe
printfn "%d users retweet 2 tweets of per user is %f and live connection user is %i" N time_retweet retweetCount
printfn "query subscribe of %d users, time is %f and live connection user is %i" N time_querysubscribe qsbCount
printfn "query %d hashtag, time is %f and live connection user is %i" N time_queryHashtag hashtagCount
printfn "query %d mention, time is %f and live connection user is %i" N time_queryMention mentionCount
printfn "The time of %d random operations is %f and live connection user is %i" N time_random randomCount
system.Terminate() |> ignore
0 // return an integer exit code

