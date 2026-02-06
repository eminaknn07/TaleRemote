# TaleRemote - Adaptive Remote Desktop Tool

TaleRemote is a professional-grade remote desktop management tool developed using C# and .NET. It is specifically optimized for technical support and remote administration, utilizing a **Reverse Connection** architecture to bypass firewall and NAT complexities without requiring manual port forwarding on the client side.



## 🚀 Core Features

* **Adaptive Quality Control:** Dynamically adjusts JPEG compression levels based on real-time network conditions (FPS-based feedback). It ensures a smooth experience even on low-bandwidth connections.
* **Reverse Connection Mappings:** Designed to work seamlessly with tunneling services like Ngrok or static IPs, allowing support staff to reach clients behind strict firewalls.
* **Full Input Synchronization:** Supports real-time mouse movements (clicks, right-clicks, drag-and-drop) and keyboard injections.
* **Optimized Streaming:** Uses 50% display scaling and advanced GDI+ compression to minimize latency and data usage.
* **Live Analytics Dashboard:** Monitor connected PC details (Hostname, Username), real-time FPS, and data transfer rates (KB/s).

## 🛠️ Technical Overview

The project consists of two main components:
1. **ServerApp:** The controller interface used by the technician to accept connections and manage the remote session.
2. **ClientApp:** A lightweight agent executed by the end-user to stream their screen and execute incoming commands.



### Tech Stack
* **Language:** C# (.NET Framework / .NET 6+)
* **Networking:** TCP/IP Sockets (`System.Net.Sockets`)
* **Graphics:** `System.Drawing` & GDI+
* **Interoperability:** Windows API (`User32.dll`) for mouse and keyboard simulation.

## 📦 Getting Started

1. Clone the repository to your local machine.
2. Set up a TCP tunnel for your server (e.g., via Ngrok) on port `5000`:
   ```bash
   ngrok tcp 5000
Update the IP and Port constants in the ClientApp source code with your server's public address.

Launch the ServerApp first, then run the ClientApp.

📋 Roadmap
[ ] AES-256 End-to-End Encryption

[ ] File Transfer Module (Drag & Drop support)

[ ] Remote Audio Streaming

[ ] Multi-client Session Management
