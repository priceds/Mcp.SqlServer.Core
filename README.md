<div align="center">

# 🔌 Mcp.SqlServer.Core
### The .NET 10 Model Context Protocol Server for SQL Server

<img src="https://readme-typing-svg.herokuapp.com?font=JetBrains+Mono&size=22&duration=3000&pause=1000&center=true&vCenter=true&width=700&lines=Connect+LLMs+to+SQL+Server+Securely.;Schema+Aware.+Read-Only+Enforced.;Built+on+.NET+10." />

<br/>

[![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/Protocol-MCP-black?style=for-the-badge&logo=anthropic&logoColor=white)](https://modelcontextprotocol.io/)
[![SQL Server](https://img.shields.io/badge/Backend-SQL_Server-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)

<br/>

> **"Bridging the gap between Generative AI reasoning and Relational Data structures."**

</div>

---

## ⚡ Mission

**SqlGateway.MCP** is a high-performance implementation of the **Model Context Protocol (MCP)**, designed to give LLMs (like Claude, Gemini, etc.) direct, structured access to Microsoft SQL Server databases.

Unlike generic connectors, this server is built with **safety-first architecture**. It exposes database schemas and data to AI agents while strictly enforcing read-only boundaries at the application layer.

---

## 🏗️ System Architecture

This server acts as a translation layer between the JSON-RPC based MCP protocol and T-SQL.

```text
   ┌──────────────┐       ┌──────────────┐       ┌────────────────────┐
   │              │       │              │       │                    │
   │   AI Agent   │ ◄───► │  MCP Host    │ ◄───► │   SqlGateway.MCP   │
   │  (LLM/User)  │       │ (Claude/IDE) │       │     (.NET 10)      │
   │              │       │              │       │                    │
   └──────────────┘       └──────────────┘       └─────────┬──────────┘
                                                           │
                                                [ Safe T-SQL Tunnel ]
                                                           │
                                                           ▼
                                                 ┌────────────────────┐
                                                 │                    │
                                                 │   SQL Server DB    │
                                                 │   (Read Only)      │
                                                 │                    │
                                                 └────────────────────┘
