# PA2 Team1 Project README

## Date: November 2, 2023

## Team Information

- **Harshit Lohaan**
  - UFID: 7615-8695
  - Email: [h.lohaan@ufl.edu](mailto:h.lohaan@ufl.edu)

- **Vashist Hegde**
  - UFID: 8721-8376
  - Email: [vashisthegde@ufl.edu](mailto:vashisthegde@ufl.edu)

- **Rahul Mora**
  - UFID: 4577-9236
  - Email: [mora.rahul@ufl.edu](mailto:mora.rahul@ufl.edu)

- **Sushmitha Kasireddy**
  - UFID: 5336-8700
  - Email: [s.kasireddy@ufl.edu](mailto:s.kasireddy@ufl.edu)

## How to Run the Code

Ensure the .NET SDK is installed on your system. Follow these commands in your project directory:

1. **To install Akka**:
   ```bash
   dotnet new install "Akka.templates::*"

2. **To run the project**:
    ```bash
   dotnet run {number of nodes} {number of messages}

## Description of Code Structure

The project is structured around two main actors: Node and Simulator.

### Node Actor

Represents each node within the Chord ring. Key properties include iD, FingerTable, predecessorId, successorId, hopCount, and mssgCount. Implements methods for data requests, finger fixing, stabilization, and more.

### Simulator Actor

Facilitates the simulation of the Chord ring. Manages hopCount, completedNodes, and nodeIdHopsDirectory. It includes methods for creating and joining the Chord ring.

## Team Contributions

- Sushmitha Kasireddy: Tested functionality and assisted with the report.
- Rahul Mora: Developed node joining algorithms and fingertable updates.
- Vashist Hegde: Conducted experiments with the code and was responsible for data collection, plotting, and reporting.
- Harshit Lohaan: Designed actor and node simulators for simulation processes, including data export.

## Execution Results

No abnormal results encountered. A noted limitation is the graceful termination when input exceeds capacity, with support for up to 1000 nodes. Outputs and plots are included to demonstrate node efficiency and behavior.
