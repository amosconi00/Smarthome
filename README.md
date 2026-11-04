# Smart Home Energy Monitoring

An intelligent Smart Home Energy Monitoring platform designed to monitor, analyze, and optimize household electricity consumption.

The system combines real-time energy monitoring with Artificial Intelligence techniques to identify individual appliance consumption from aggregate power measurements using **Non-Intrusive Load Monitoring (NILM)**.

## Key Features

- **Real-Time Energy Monitoring** – Collect and visualize power consumption and electrical telemetry.
- **Appliance Management** – Manage household appliances, categories, and their electrical signatures.
- **NILM Detection** – Identify and estimate individual appliance consumption using AI-based models.
- **Seq2Point Deep Learning** – Train appliance-specific models using TensorFlow and export them to ONNX.
- **Energy Analytics** – Analyze daily and monthly consumption and monitor energy usage.
- **Electricity Tariffs** – Configure electricity tariffs and support consumption cost analysis.
- **Smart Plugs & Power Meters** – Manage connected devices and monitor their measurements.
- **Data Export** – Export consumption and telemetry data for further analysis.
- **Real-Time Communication** – MQTT and SignalR integration for telemetry acquisition and live updates.

## Architecture & Technologies

| Component | Technologies |
|---|---|
| Frontend | Blazor WebAssembly, C# |
| Backend | ASP.NET Core Web API, .NET 8 |
| Database | PostgreSQL, Entity Framework Core |
| Real-Time Communication | MQTT, SignalR |
| Machine Learning | Python, TensorFlow, Seq2Point |
| Model Deployment | ONNX, Microsoft.ML.OnnxRuntime |
| API Documentation | Swagger / OpenAPI |

## NILM – Non-Intrusive Load Monitoring

The NILM component uses a deep learning approach based on the Seq2Point architecture.

Instead of requiring individual power meters for every appliance, the system analyzes aggregate electricity consumption and estimates the power usage of selected appliances.

The machine learning pipeline includes:

1. Data preprocessing and time-series alignment.
2. Appliance-specific model training.
3. Model evaluation using MAE and RMSE.
4. Export of trained models to ONNX format.
5. Model inference integrated into the .NET backend.

## Project Goal

The main goal is to develop an intelligent energy monitoring platform capable of providing detailed insights into household electricity consumption, identifying the contribution of individual appliances, and supporting more informed energy management decisions.

---

*Developed as part of an academic project focused on Smart Home technologies, energy monitoring, and Artificial Intelligence.*
