# Task-queueing-Background-worker
Background Task Queue Processing App
Overview
You are required to design and implement an ASP.NET Core–based web application that
manages and processes a queue of tasks. The application must be capable of running in
different hosting environments — self-hosted (Kestrel), IIS, or Azure — while ensuring
high availability, responsiveness, and minimal resource consumption.
Scenario
• The system needs to maintain a task queue that is populated whenever certain
database tables are updated.
• These database updates are triggered by external applications/services (not by
this app itself).
• Once tasks are added to the queue, the application must process them as
quickly as possible, without significant startup delays.
• The application must therefore remain active and ready to process tasks,
avoiding idle shutdowns or long cold starts (a common issue in Azure App
Services or IIS idle timeout scenarios).
• The solution should be cost-efficient in Azure, consuming minimal resources
while still maintaining responsiveness.
Deliverables
• A short design document describing:
1. Which ASP.NET Core project type you chose and why.
2. The architecture diagram (queue producer/consumer, task flow, hosting
environment).
3. Configuration details for IIS and Azure to prevent shutdowns.
4. Strategy for minimizing Azure costs.
• A working code prototype demonstrating:
o Task queueing (simple simulated database update → task added).
o Background worker processing tasks.
o Hosting in at least one environment (local self-hosted or IIS).
