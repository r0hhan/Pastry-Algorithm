// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

#r "nuget: Akka.FSharp"
#load "utils.fsx"
open System
open Akka.FSharp
open Utils

let system = System.create "system" (Configuration.defaultConfig ())

let rnd = System.Random()

type ProcessCommmand =
| Ack of string
| AddNode of string
| Build of string * bool * array<string>
| Delivered of int
| Input of int * int
| Init of string
| InitMessage of string
| JoinNetwork of string
| Message of string * string * int
| Start of int

let mutable nodeSet = set["1"]
let mutable joinedNodes: string array = Array.empty
let mutable unjoinedNodes: string array = Array.empty
let mutable totalNodes = 0

let childActor (mailbox: Actor<_>) =
    let mutable nodeId = ""
    let mutable proxId = ""
    let mutable smallLeafSet = [| |]
    let mutable largeLeafSet = [| |]
    let mutable neighbourhoodSet = [| |]
    let mutable routingTable = Array2D.init 8 4 (fun x y -> "NULL")
    let mutable converted = 0
    let rec loop () =
        actor {
            let! msg = mailbox.Receive()
            match msg with
                | AddNode(key) ->
                    let mutable table = Array.append largeLeafSet smallLeafSet
                    table <- Array.append table [| key |]

                    table |> Array.sortInPlaceBy (fun t -> Utils.diff t nodeId)
                    smallLeafSet <- Array.empty
                    largeLeafSet <- Array.empty
                    
                    for node in table do
                        if node <> nodeId then
                            if node < nodeId && smallLeafSet.Length < 4 then
                                smallLeafSet <- Array.append smallLeafSet [|node|]
                            elif node > nodeId && largeLeafSet.Length < 4 then
                                largeLeafSet <- Array.append largeLeafSet [|node|]


                    let l = Utils.shl key nodeId
                    let dl = int(key.[l]) - 48
                    routingTable.[l, dl] <- key

                | Build(key, isLeafSet, table) ->
                    if isLeafSet then
                        let leafSet = Array.append table [| key |]
                        leafSet |> Array.sortInPlaceBy (fun t -> Utils.diff t nodeId)
                        
                        for node in leafSet do
                            if node < nodeId && smallLeafSet.Length < 4 then
                                smallLeafSet <- Array.append smallLeafSet [|node|]
                            elif node > nodeId && largeLeafSet.Length < 4 then
                                largeLeafSet <- Array.append largeLeafSet [|node|]

                        let mutable table = Array.append smallLeafSet largeLeafSet
                        for i in 0 .. 7 do
                            for j in 0 .. 3 do
                                if routingTable.[i, j] <> "NULL" && not (Array.contains routingTable.[i, j] table) then
                                    table <- Array.append table[| routingTable.[i, j] |]

                        for node in smallLeafSet do
                            let ref = system.ActorSelection("akka://system/user/" + node)
                            ref <! AddNode(nodeId)
                                
                        for node in largeLeafSet do
                            let ref = system.ActorSelection("akka://system/user/" + node)
                            ref <! AddNode(nodeId)
                        
                        for i in 0 .. 7 do
                            for j in 0 .. 3 do
                                if routingTable.[i, j] <> "NULL" then
                                    let ref = system.ActorSelection("akka://system/user/" + routingTable.[i, j])
                                    ref <! AddNode(key)
                        
                        let ref = system.ActorSelection("akka://system/user/mainActor")
                        ref <! Ack(nodeId)
                        // printfn "%A" largeLeafSet
                        
                    else
                        let l = Utils.shl nodeId key
                        let dl = int(key.[l]) - 48
                        routingTable.[l, dl] <- key

                        for node in table do
                            if node <> "NULL" then
                                let l = Utils.shl nodeId node
                                let dl = int(node.[l]) - 48
                                routingTable.[l, dl] <- node

                | Init(key) ->
                    // kept both same for routing
                    nodeId <- key
                    proxId <- key            

                | InitMessage(key) ->
                    //routing algorithm
                    let mutable distance = Utils.diff key nodeId
                    let mutable nextNode = "NULL"
                    let mutable flag = false
                    let unjoinedNode = system.ActorSelection("akka://system/user/" + key)
                    
                    // check in leaf set
                    if smallLeafSet.Length <> 0 && smallLeafSet.[smallLeafSet.Length - 1] < key && key < nodeId then
                        flag <- true
                        for node in smallLeafSet do
                            if Utils.diff key node < distance then
                                distance <- Utils.diff key node
                                nextNode <- node
                    elif largeLeafSet.Length <> 0 && key < largeLeafSet.[largeLeafSet.Length - 1] && key > nodeId then
                        flag <- true
                        for node in largeLeafSet do
                            if Utils.diff key node < distance then
                                distance <- Utils.diff key node
                                nextNode <- node
                    if flag then
                        if nextNode = "NULL" then
                            let mutable leafSet = smallLeafSet
                            for node in largeLeafSet do
                                leafSet <- Array.append leafSet [| node |]
                            unjoinedNode <! Build(nodeId, true, leafSet)
                        else
                            // printfn "Leaf Set: Next node - %s Destination - %s Current Node - %s" nextNode key nodeId
                            let ref = system.ActorSelection("akka://system/user/" + nextNode)
                            ref <! InitMessage(key)

                    else
                        let l = Utils.shl key nodeId
                        let dl = int(key.[l]) - 48
                        if routingTable.[l, dl] <> "NULL" then
                            // printfn "Routing Table: Next node - %s Destination - %s Current Node - %s" routingTable.[l ,dl] key nodeId
                            
                            let ref = system.ActorSelection("akka://system/user/" + routingTable.[l, dl])
                            ref <! InitMessage(key)
                            
                            // send row of routing table
                            let mutable routingRow: string array = Array.empty
                            for j in 0 .. 3 do
                                routingRow <- Array.append routingRow [| routingTable.[l, j] |]
                            unjoinedNode <! Build(nodeId, false, routingRow)
                        
                        else
                            distance <- Utils.diff key nodeId
                            let mutable flag = false
                            for i in smallLeafSet do
                                if (not flag) && (distance > (Utils.diff key i)) && ((Utils.shl key i) >= l) then
                                    // printfn "Union Leaf: Next node - %s Destination - %s Current Node - %s" i key nodeId
                                    let l = Utils.shl key nodeId

                                    // send row of routing table
                                    let mutable routingRow: string array = Array.empty
                                    for j in 0 .. 3 do
                                        routingRow <- Array.append routingRow [| routingTable.[l, j] |]
                                    unjoinedNode <! Build(nodeId, false, routingRow)
                                    
                                    let ref = system.ActorSelection("akka://system/user/" + i)
                                    ref <! InitMessage(key)
                                    flag <- true

                            for i in largeLeafSet do
                                if (not flag) && (distance > (Utils.diff key i)) && ((Utils.shl key i) >= l) then
                                    // printfn "Union Leaf: Next node %s" i
                                    let l = Utils.shl key nodeId
                                    
                                    // send row of routing table
                                    let mutable routingRow: string array = Array.empty
                                    for j in 0 .. 3 do
                                        routingRow <- Array.append routingRow [| routingTable.[l, j] |]
                                    unjoinedNode <! Build(nodeId, false, routingRow)
                                    
                                    let ref = system.ActorSelection("akka://system/user/" + i)
                                    ref <! InitMessage(key)
                                    flag <- true

                            for i in 0 .. 7 do
                                for j in 0 .. 3 do
                                    if (not flag) && (routingTable.[i ,j] <> "NULL") then
                                        if (distance > (Utils.diff key routingTable.[i ,j])) && ((Utils.shl key routingTable.[i ,j]) >= l) then
                                            // printfn "Union Routing: Next node %s" routingTable.[i ,j]
                                            let l = Utils.shl key nodeId

                                            // send row of routing table
                                            let mutable routingRow: string array = Array.empty
                                            for j in 0 .. 3 do
                                                routingRow <- Array.append routingRow [| routingTable.[l, j] |]
                                            unjoinedNode <! Build(nodeId, false, routingRow)

                                            let ref = system.ActorSelection("akka://system/user/" + routingTable.[i ,j])
                                            ref <! InitMessage(key)
                                            flag <- true

                            if not flag then
                                // printfn "Donezo. Closest Destination is %s" nodeId
                                let leafSet = Array.append smallLeafSet largeLeafSet
                                unjoinedNode <! Build(nodeId, true, leafSet)

                | JoinNetwork(x) ->
                    let joinedNode = system.ActorSelection("akka://system/user/" + x)
                    joinedNode <! InitMessage(nodeId)

                | Message(key, msg, hops) ->
                    //routing algorithm
                    let mutable distance = Utils.diff key nodeId

                    if key = nodeId then
                        // printfn "Donezo. Destination is %s" nodeId
                        let ref = system.ActorSelection("akka://system/user/mainActor")
                        ref <! Delivered(hops)
                    else
                        if Utils.diff key nodeId <= "00000010" && (nodeSet.Contains key) then
                            // printfn "Leaf Set: Next node %s" key
                            let ref = system.ActorSelection("akka://system/user/" + key)
                            ref <! Message(key, msg, hops+1)

                        else
                            let l = Utils.shl key nodeId
                            let dl = int(key.[l]) - 48
                            if routingTable.[l, dl] <> "NULL" then
                                // printfn "Routing Table: Next node %s" routingTable.[l ,dl]
                                
                                let ref = system.ActorSelection("akka://system/user/" + routingTable.[l, dl])
                                ref <! Message(key, msg, hops+1)
                            
                            else
                                distance <- Utils.diff key nodeId
                                let mutable flag = false
                                for i in smallLeafSet do
                                    if (not flag) && (distance > (Utils.diff key i)) && ((Utils.shl key i) >= l) then
                                        // printfn "Union Leaf: Next node %s" i
                                        let ref = system.ActorSelection("akka://system/user/" + i)
                                        ref <! Message(key, msg, hops+1)
                                        flag <- true

                                for i in largeLeafSet do
                                    if (not flag) && (distance > (Utils.diff key i)) && ((Utils.shl key i) >= l) then
                                        // printfn "Union Leaf: Next node %s" i
                                        let ref = system.ActorSelection("akka://system/user/" + i)
                                        ref <! Message(key, msg, hops+1)
                                        flag <- true 

                                for i in 0 .. 7 do
                                    for j in 0 .. 3 do
                                        if (not flag) && (routingTable.[i ,j] <> "NULL") then
                                            if (distance > (Utils.diff key routingTable.[i ,j])) && ((Utils.shl key routingTable.[i ,j]) >= l) then
                                                // printfn "Union Routing: Next node %s" routingTable.[i ,j]
                                                let ref = system.ActorSelection("akka://system/user/" + routingTable.[i ,j])
                                                ref <! Message(key, msg, hops+1)
                                                flag <- true
                                                    
                                for i in neighbourhoodSet do
                                    if (not flag) && (distance > (Utils.diff key i)) && ((Utils.shl key i) >= l) then
                                        // printfn "Neighbourhood: Next node %s" i
                                        let ref = system.ActorSelection("akka://system/user/" + i)
                                        ref <! Message(key, msg, hops+1)
                                        flag <- true
                                
                                if not flag then
                                    // printfn "Donezo. Closest Destination to %s is: %s" key nodeId
                                    let ref = system.ActorSelection("akka://system/user/mainActor")
                                    ref <! Delivered(hops)
                                    
                | Start(value) ->
                    for i in 1 .. value do
                        let destination = Utils.getRandomNumber rnd joinedNodes.Length
                        mailbox.Self <! Message(joinedNodes.[destination], "abc", 0)

                | _ ->
                printfn "Invalid"
                    

            return! loop ()
        }
    loop ()

let mainActor (mailbox: Actor<_>) =
    let mutable totalHops = 0
    let mutable totalMessages = 0
    let mutable messagesDelivered = 0
    let mutable acks = 0
    let mutable numNodes = 0
    let mutable numReq = 0
    let rec loop () =
        actor {
            let! msg = mailbox.Receive()
            match msg with
                | Ack(nodeId) ->
                    acks <- acks + 1
                    printfn "%i" acks
                    if acks = numNodes then
                        for node in joinedNodes do
                            let ref = system.ActorSelection("akka://system/user/" + node)
                            ref <! Start(numReq)
                    else
                        let unjoinedNodeIdx = Utils.getRandomNumber rnd unjoinedNodes.Length
                        let joinedNodeIdx = Utils.getRandomNumber rnd joinedNodes.Length
                        
                        let unjoinedNode = system.ActorSelection("akka://system/user/" + unjoinedNodes.[unjoinedNodeIdx])
                        let joinedNode = joinedNodes.[joinedNodeIdx]
                        unjoinedNode <! JoinNetwork(joinedNode)

                        joinedNodes <- Array.append joinedNodes [| unjoinedNodes.[unjoinedNodeIdx] |]
                        unjoinedNodes <- unjoinedNodes |> Array.filter ((<>) unjoinedNodes.[unjoinedNodeIdx])
                    
                | Input(nodes, req) ->
                    numNodes <- nodes
                    numReq <- req
                    // Create Child Actor
                    for i in 1 .. numNodes do
                        let nodeId = Utils.convertToQuart i
                        nodeSet <- nodeSet.Add(nodeId)
                        unjoinedNodes <- Array.append unjoinedNodes [| nodeId |]
                        // printfn "%s %s" nodeId proxId
                        let ref = spawn system nodeId childActor
                        ref <! Init(nodeId)

                    nodeSet <- nodeSet.Remove("1")

                    let firstNode = Utils.getRandomNumber rnd unjoinedNodes.Length
                    joinedNodes <- Array.append joinedNodes [| unjoinedNodes.[firstNode] |]
                    unjoinedNodes <- unjoinedNodes |> Array.filter ((<>) unjoinedNodes.[firstNode])
                    mailbox.Self <! Ack(unjoinedNodes.[firstNode])
                    
                    totalMessages <- numNodes * numReq

                | Delivered(hops) ->
                    messagesDelivered <- messagesDelivered + 1
                    totalHops <- totalHops + hops
                    if messagesDelivered = totalMessages then
                        printfn "Network size: %i" numNodes
                        printfn "Average hops: %f" (float(totalHops)/float(totalMessages))
                
                | _ ->
                    printfn "Invalid"

            return! loop ()
        }
    loop ()

let mainActorRef =
    spawn system "mainActor" mainActor

let argv = fsi.CommandLineArgs.[1..]

mainActorRef <! Input(argv.[0] |> int, argv.[1] |> int)

totalNodes <- argv.[0] |> int

System.Console.ReadLine() |> ignore