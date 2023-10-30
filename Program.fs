module Main
open Akka.FSharp
open System.Threading 
open Akka.Actor
open System
open Akka.FSharp
open System.Security.Cryptography
open System.Collections.Generic
open System.IO
open FSharp.Data



// -------------------------------Acktor Messages---------------------------------
type FixFinger = {
    TargetFinger : int 
    Next : int 
    RequesterNodeId : int
}

type FixFingerRes = {
    TargetFinger : int 
    Next : int 
    ResultNodeId : int
}

type RequestInfo = {
    TargetKey : int
    TargetKeyIndex : int
    RequesterNodeId : int
    Hop : int
}

type ResultInfo = {
    TargetKey : int
    TargetKeyIndex : int
    ResultNodeId : int
    Hop : int
}

type Notify = {
    PotentialPredecessor : int
}

type Stabilize = {
    RequesterNodeId : int
}

type StabilizeResponse = {
    PotentialSuccessor : int
}

type Init = {
    RandomSearchNodeId : int
    FirstOrNot : bool
}

type NodeMessage =
    | FixFinger of FixFinger
    | FixFingerRes of FixFingerRes
    | RequestInfo of RequestInfo
    | ResultInfo of ResultInfo
    | Notify of Notify
    | Stabilize of Stabilize
    | StabilizeResponse of StabilizeResponse
    | Init of Init
    | RequestData of string
    | Request of int

type SimulatorMessage =
    | UpdateHops of (int*int)


// -------------------------------global variables---------------------------------

let mutable numNodes = 0|> int
let mutable numRequests = 0 |> int
let system = ActorSystem.Create("ChordSimulation")
let rand = System.Random()
let m = 20
let startTime = TimeSpan.FromSeconds(0.05)
let delayTimeForStabilize = TimeSpan.FromSeconds(0.01)
let delayTimeForFixfinger = TimeSpan.FromSeconds(0.02)
let mutable nodeIdList = []
let mutable nodesList = []

// -------------------------------other functions---------------------------------
let ranStr n = 
    let r = Random()
    let chars = Array.concat([[|'a' .. 'z'|];[|'0' .. '9'|]])
    let sz = Array.length chars in
    String(Array.init n (fun _ -> chars.[r.Next sz]))


// Function to append data to the CSV file
let appendDataToCsv numNodes numRequests avgHop =
    let filePath = "./output.csv"
    
    // Format the data as a CSV line
    let csvLine = sprintf "%d,%d,%f" numNodes numRequests avgHop

    use file = new System.IO.StreamWriter(filePath, true)
    file.WriteLine(csvLine)
    file.Close()


// -------------------------------Node Actor---------------------------------

let Node (nodeId:int) (mailbox: Actor<_>) = 
    let Id = nodeId
    let mutable currentFinger = 1
    let mutable FingerTable = Array.create m [||]
    let mutable predecessorId = -1
    let mutable successorId = nodeId
    let mutable hopCount = 0
    let mutable mssgCount = 0
    
    let rec loop() = actor{

        let! message = mailbox.Receive()

        match message with
        | RequestData requestData->
            let content = System.Text.Encoding.ASCII.GetBytes requestData
            let bytes = SHA1.Create().ComputeHash(content)
            let targetKey = (int bytes.[bytes.Length-1]) + (int bytes.[bytes.Length-2]*(pown 2 8))
            let request = {TargetKey=targetKey; TargetKeyIndex=(-1); RequesterNodeId=Id; Hop=0}
            mailbox.Self<! RequestInfo request

        | Request request->
            // Request 1 triggers Stabilize
            if request=1 then 
                let successorNode = system.ActorSelection("akka://ChordSimulation/user/"+("Node:" + (string successorId)))
                let stabilize = {RequesterNodeId=Id}
                successorNode <! Stabilize stabilize
            // Request 2 triggers FixFingers
            else 
                if currentFinger>m then
                    currentFinger <- 1
                let fixFingerRequest = {TargetFinger=FingerTable.[currentFinger-1].[0]; Next=currentFinger; RequesterNodeId=Id}
                mailbox.Self <! FixFinger fixFingerRequest
                currentFinger <- currentFinger+1

        | RequestInfo requestInfo ->
            let targetKey = requestInfo.TargetKey
            let requesterNodeId = requestInfo.RequesterNodeId
            let targetKeyIndex = requestInfo.TargetKeyIndex
            let mutable hop = requestInfo.Hop
   
            let requesterNode = system.ActorSelection("akka://ChordSimulation/user/"+( "Node:" + (string requesterNodeId)))
            if hop<>(-1) then
                hop <- hop+1
                
            if Id=successorId || targetKey=Id then 
                let resultInfo = {TargetKey=targetKey; TargetKeyIndex=targetKeyIndex;ResultNodeId=Id; Hop=hop}
                requesterNode <! ResultInfo resultInfo       

            elif (Id<successorId && targetKey>Id && targetKey<=successorId)||(Id>successorId && (targetKey>Id || targetKey<=successorId)) then
                let resultInfo = {TargetKey=targetKey; TargetKeyIndex=targetKeyIndex; ResultNodeId=successorId; Hop=hop}
                requesterNode <! ResultInfo resultInfo

            else 
                let mutable countdown = m-1
                let mutable breakLoop = false

                while countdown>=0 && not breakLoop do
                    let tempFingerNodeId = FingerTable.[countdown].[1]

                    if (Id<tempFingerNodeId && (targetKey>=tempFingerNodeId || Id>targetKey)) 
                       ||(Id>tempFingerNodeId && (targetKey>=tempFingerNodeId && Id>targetKey)) then
                        let tempFingerNode = system.ActorSelection("akka://ChordSimulation/user/"+( "Node:" + (string tempFingerNodeId)))
                        let forwardRequest = {TargetKey=targetKey; TargetKeyIndex=targetKeyIndex; RequesterNodeId=requesterNodeId; Hop=hop}
                        tempFingerNode <! RequestInfo forwardRequest
                        breakLoop <- true

                    elif Id=tempFingerNodeId then
                        let resultInfo = {TargetKey=targetKey; TargetKeyIndex=targetKeyIndex;ResultNodeId=tempFingerNodeId; Hop=hop}
                        requesterNode <! ResultInfo resultInfo
                    countdown <- countdown-1

        | ResultInfo resultInfo ->
            let targetKeyIndex = resultInfo.TargetKeyIndex
            let resultNodeId = resultInfo.ResultNodeId
            let hop = resultInfo.Hop 

            if hop<>(-1) then
                hopCount <- hopCount + hop
                mssgCount <- mssgCount + 1

                if mssgCount=numRequests then
                    let SimulateNode = system.ActorSelection("akka://ChordSimulation/user/Simulate")
                    SimulateNode <! UpdateHops (Id,hopCount)
                    // printfn "Node-%d, Avg hops%f" Id ((float hopCount)/ (float numRequests)) 
                
            elif targetKeyIndex<>(-1) then

                if targetKeyIndex=1 then
                    successorId <- resultNodeId
                FingerTable.[targetKeyIndex-1].[1] <- resultNodeId

        | FixFinger fixFinger ->
            let targetFinger = fixFinger.TargetFinger
            let Next = fixFinger.Next
            let requesterNodeId = fixFinger.RequesterNodeId

            let requesterNode = system.ActorSelection("akka://ChordSimulation/user/"+( "Node:" + (string requesterNodeId)))
            if Id=successorId || targetFinger=Id then 
                let resultInfo = {TargetFinger=targetFinger; Next=Next;ResultNodeId=Id}
                requesterNode <! FixFingerRes resultInfo

            elif (Id<successorId && targetFinger>Id && targetFinger<=successorId)
                ||(Id>successorId && (targetFinger>Id || targetFinger<=successorId)) then
                let resultInfo = {TargetFinger=targetFinger; Next=Next;ResultNodeId=successorId}
                requesterNode <! FixFingerRes resultInfo

            else 
                let mutable countdown = m-1
                let mutable breakLoop = false
                while countdown>=0 && not breakLoop do
                    let tempFingerNodeId = FingerTable.[countdown].[1]
                    if (Id<tempFingerNodeId && (targetFinger>=tempFingerNodeId || Id>targetFinger)) 
                       ||(Id>tempFingerNodeId && (targetFinger>=tempFingerNodeId && Id>targetFinger)) then
                        let tempFingerNode = system.ActorSelection("akka://ChordSimulation/user/"+ ( "Node:" + (string tempFingerNodeId)))
                        let forwardRequest = {TargetFinger=targetFinger; Next=Next; RequesterNodeId=requesterNodeId}
                        tempFingerNode <! FixFinger forwardRequest
                        breakLoop <- true
                    elif Id=tempFingerNodeId then
                        let resultInfo = {TargetFinger=targetFinger; Next=Next;ResultNodeId=tempFingerNodeId}
                        requesterNode <! FixFingerRes resultInfo
                    countdown <- countdown-1

        | FixFingerRes fixFingerRes ->
            let Next = fixFingerRes.Next
            let resultNodeId = fixFingerRes.ResultNodeId
            FingerTable.[Next-1].[1] <- resultNodeId

        | Stabilize stabilize ->
            let requesterNodeId = stabilize.RequesterNodeId
            let requesterNode = system.ActorSelection("akka://ChordSimulation/user/"+("Node:" + (string requesterNodeId)))
            if predecessorId=(-1) then
                predecessorId <- Id
            let stabilizeResponse = {PotentialSuccessor=predecessorId}
            requesterNode <! StabilizeResponse stabilizeResponse

        | StabilizeResponse stabilizeResponse ->
            let potentialSuccessor = stabilizeResponse.PotentialSuccessor
            if potentialSuccessor<>successorId then
                if Id=successorId then
                    successorId <- potentialSuccessor
                if (Id<successorId && potentialSuccessor>Id && potentialSuccessor<successorId) 
                   || (Id>successorId && (potentialSuccessor>Id || potentialSuccessor<successorId))then
                    successorId <- potentialSuccessor
            let notify = {PotentialPredecessor=Id}
            let successorNode = system.ActorSelection("akka://ChordSimulation/user/"+ ("Node:" + (string successorId)))
            successorNode <! Notify notify

        | Notify notify ->
            let potentialPredecessor = notify.PotentialPredecessor
            if predecessorId<>potentialPredecessor then
                if Id=predecessorId || predecessorId=(-1) then
                    predecessorId <- potentialPredecessor 
                if (predecessorId<Id && potentialPredecessor>predecessorId && potentialPredecessor<Id) 
                   ||(predecessorId>Id && (potentialPredecessor>predecessorId || potentialPredecessor<Id))then
                    predecessorId <- potentialPredecessor 

        | Init initialization ->
            let randomSearchNodeId = initialization.RandomSearchNodeId
            let firstOrNot = initialization.FirstOrNot
            for i in 1..m do
                let insertKey = (Id + pown 2 (i-1)) % (pown 2 m)
                FingerTable.[i-1] <- [|insertKey;Id|]
            if not firstOrNot then
                let randomSearchNode = system.ActorSelection("akka://ChordSimulation/user/"+("Node:" + (string randomSearchNodeId)))
                for i in 1..m do
                    let requestKey = (Id + pown 2 (i-1)) % (pown 2 m)
                    let requestInfo = {TargetKey=requestKey; TargetKeyIndex=i; RequesterNodeId=Id; Hop=(-1)}
                    randomSearchNode <! RequestInfo requestInfo         

        return! loop()
    }
    loop()


// -------------------------------Simulator Actor---------------------------------
let SimulateNode (mailbox: Actor<_>) = 
    let mutable hopCount = 0
    let mutable completedNodes = 0
    let nodeIdHopsDictionary = new Dictionary<int, int>()

    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with
        | UpdateHops nodehops ->
            let (N,H) = nodehops
            nodeIdHopsDictionary.Add(N, H)
            hopCount <- hopCount+H
            completedNodes <- completedNodes+1
            if completedNodes=numNodes then
                for kvp in nodeIdHopsDictionary do
                    printfn "Node %d: Average Hops = %f" kvp.Key ( (float kvp.Value)/ (float numRequests) ) 
                let avgHop = (float hopCount) / (float (numNodes*numRequests))
                printfn "The average No. of hops per node per message is %f hops" avgHop
                    
                appendDataToCsv numNodes numRequests avgHop

        return! loop()
    }
    loop()
// --------------------------------parent functions---------------------------------
let Create(node,nodeId) =
    printfn "Creating First Node with Id %d..." nodeId
    node <! Init { RandomSearchNodeId=(-1); FirstOrNot=true }

    // Add Node to nodesList
    nodeIdList <- nodeIdList @ [nodeId]
    // Periodically Call Stabilize()
    system.Scheduler.ScheduleTellRepeatedly(startTime,delayTimeForStabilize,node,Request 1)
    // Periodically Call FixFingers()
    system.Scheduler.ScheduleTellRepeatedly(startTime,delayTimeForFixfinger,node,Request 2)
    //Pause for 25 ms
    Thread.Sleep(25)

let Join(node,nodeId) =
    printfn "Adding Node with Id %d..." nodeId
    let rnd = rand.Next(nodeIdList.Length)
    let rndNodeIndex = nodeIdList.[rnd]
    node <! Init {RandomSearchNodeId=rndNodeIndex; FirstOrNot=false}

    // Add Node to nodesList
    nodeIdList <- nodeIdList @ [nodeId]
    // Periodically Call Stabilize()
    system.Scheduler.ScheduleTellRepeatedly(startTime,delayTimeForStabilize,node,Request 1)
    // Periodically Call FixFingers()
    system.Scheduler.ScheduleTellRepeatedly(startTime,delayTimeForFixfinger,node,Request 2)
    //Pause for 25 ms
    Thread.Sleep(25)

let CreateChord() =

            for _ in 1..numNodes do

                // Generate a Random m bit node Identifier
                let mutable nodeId = rand.Next(pown 2 m)

                // If randomly generated Node ID is already assigned to a Node, try a new identifier 
                while List.contains nodeId nodeIdList do
                    nodeId <- rand.Next(pown 2 m)

                // Create a New Node Actor to simulate every Node
                let node = spawn system ("Node:" + (string nodeId)) <| Node nodeId

                // Add Node to nodesList
                nodesList <- nodesList @ [node]

                if nodeIdList.Length=0 then
                    Create(node,nodeId)
                else
                    Join(node,nodeId)

            printfn "\n----Chord Ring Created with %d Nodes----\n" numNodes


// ---------------------------------Main Function---------------------------------
[<EntryPoint>]
let main argv =
    // Check if there are exactly two command-line arguments
    numNodes <- int argv.[0] 
    numRequests <- int argv.[1] 
    printfn "Simulating Chord Protocol for %d nodes, where each node sends %d requests....." numNodes numRequests
    
    //Create the Chord Ring
    CreateChord()
    printfn "Simulation Start -> Each Node Sends %d Lookup Requests" numRequests
    spawn system "Simulate" <| SimulateNode |> ignore
    // Pause for 1s
    Thread.Sleep(10000)

    for _ in 1..numRequests do
        for nodeIndex in 1..numNodes do
            let data = ranStr 6
            nodesList.[nodeIndex-1] <! RequestData data
        Thread.Sleep(1000)
    0