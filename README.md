# Pastry-Algorithm

Rohit Choudhari   &emsp;  &nbsp; Rohan Hemant Wanare  
[@InfernapeXavier](https://github.com/InfernapeXavier)   &emsp; [@r0hhan](https://github.com/r0hhan)



> Inputs: numNodes, numRequests <br>
> Requirements: `Akka.Fsharp` <br>
> Run: `dotnet fsi --langverion:preview proj3.fsx numNodes numRequests` <br>


### Pastry


#### What is working

  - We successfully implemented Pastry APIs for network join and routing as described in the Pastry paper.
  - We chose the value of `b=2`, hence our NodeIDs follow the Quarternary system.
  - In the first phase, each node gets added to the network by sending a "JoinNetwork" message to a node that is already part of the network.
  - Once the network is formed, each node starts sending a placeholder message to a randomly chosen peer.
  - We keep track of the hops taken by each message and once each message has been delivered the Average number of hops is printed out.

#### What is the largest network you managed to deal with

We managed to create a Pastry network of 65535 nodes.
Values larger than 65535 lie beyond the valid range of node IDs randomly generated by our application.
We can see our Pastry network delivers messages within expected number of hops. 

| Network Size      | Average Hops      | Expected  |
|--------------     |--------------     |---------  |
| 100               | 3.715             | 4         |
| 500  	            | 4.468             | 5         |
| 1000              | 4.332             | 5         |
| 10000             | 6.184             | 7         |
| 25000             | 7.148             | 8         |
| 50000             | 7.067             | 8         |
| 60000             | 7.048             | 8         |
| 65535             | 7.040             | 8         |
