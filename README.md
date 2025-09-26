<div align="center">

# 🛰️ EDCL GPS API — Fleet GPS Telematics & Ingestion Platform

**Platform Microservices Penyerapan & Observabilitas Telemetri GPS Armada Truk Real-Time Berstandar *Enterprise* untuk Ekosistem Logistik Toyota Motor Manufacturing Indonesia (TMMIN).**

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![YARP](https://img.shields.io/badge/YARP-API_Gateway-0078D4?style=for-the-badge&logo=microsoft&logoColor=white)](https://microsoft.github.io/reverse-proxy/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16_Partitioned-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org)
[![MongoDB](https://img.shields.io/badge/MongoDB-7.0_TTL_Logs-47A248?style=for-the-badge&logo=mongodb&logoColor=white)](https://www.mongodb.com)
[![Redis](https://img.shields.io/badge/Redis-7.2_Cache_%26_Lock-DC382D?style=for-the-badge&logo=redis&logoColor=white)](https://redis.io)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3.13_Topic_DLX-FF6600?style=for-the-badge&logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com)
[![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-Tracing_%26_Metrics-425CC7?style=for-the-badge&logo=opentelemetry&logoColor=white)](https://opentelemetry.io)
[![Prometheus](https://img.shields.io/badge/Prometheus-Metrics_Scraping-E6522C?style=for-the-badge&logo=prometheus&logoColor=white)](https://prometheus.io)
[![Grafana](https://img.shields.io/badge/Grafana-Monitoring_Dashboard-F46800?style=for-the-badge&logo=grafana&logoColor=white)](https://grafana.com)

</div>

---

## 📖 Ringkasan Eksekutif

**EDCL GPS API** (`EDCLGPSAPI`) adalah platform microservices terdistribusi (*cloud-native telemetry platform*) yang dirancang khusus untuk menangani penyerapan (*ingestion*), standardisasi, penyimpanan terpartisi, dan pengaliran (*streaming*) data koordinat pelacakan GPS armada truk logistik pemasok komponen suku cadang secara *real-time*.

Platform ini beroperasi di garis terdepan (*Public Edge Ingress*) untuk mengumpulkan data telemetri dari beragam vendor GPS pihak ketiga (*Hino Connect, Jitra GPS, Puninar, EasyGo, dll.*) melalui internet publik, menormalkan struktur data yang heterogen melalui *dynamic mapping engine*, lalu mengalirkannya secara andal ke **`EDCL-Web-API`** yang berada di dalam **Jaringan Privat / Intranet TMMIN (On-Premises Plant Core)**.

### Nilai Strategis Pemisahan Service:
1. **Isolasi Beban Write-Heavy**: Ratusan armada truk logistik mengirimkan titik koordinat setiap 1 menit (akumulasi jutaan rekam jejak GPS per hari). Pemisahan ke platform ini menjamin server database transaksional inti di pabrik TMMIN (*SQL Server*) terbebas 100% dari beban *I/O disk* koordinat mentah.
2. **Keamanan Korporat (Zero-Trust Isolation)**: Server pabrik TMMIN tidak membuka port inbound publik ke internet untuk vendor GPS. Seluruh penyerapan data berlangsung di lingkungan terisolasi, kemudian dialirkan melalui jalur privat terenkripsi (*TLS/VPN/AMQPS*).
3. **Agilitas Integrasi Vendor (Zero-Downtime Deployment)**: Struktur data vendor pihak ketiga bervariasi dan dapat berubah sewaktu-waktu. Mesin *dynamic mapping* berbasis database (`m_gps_vendor_mapping`) memungkinkan penambahan vendor baru tanpa memerlukan *re-compilation* maupun *deployment downtime*.

---

## 🏛️ Arsitektur Platform Microservices

Platform ini dibangun dengan prinsip **Lean Microservices Architecture (Pure Delivery-Centric Model)**, memisahkan domain tanggung jawab ke dalam 3 layanan inti independen yang terisolasi (*self-contained*) dan 1 Ingress API Gateway:

```
                               ┌────────────────────────────────────────────────────────┐
                               │       Klien Eksternal / Web App / Mobile / Vendor       │
                               └───────────────────────────┬────────────────────────────┘
                                                           │ HTTPS
                                                           ▼
                               ┌────────────────────────────────────────────────────────┐
                               │           YARP API GATEWAY (:5000 / :8080)             │
                               │  Rate Limiting · OpenTelemetry · X-Trace-Id · Routing   │
                               └───────┬────────────────────────────────────────┬───────┘
                                       │                                        │
              ┌────────────────────────┘                                        └───────────────────────┐
              ▼                                                                                         ▼
   ┌──────────────────────┐                                                                  ┌──────────────────────┐
   │  VendorMaster.Api    │                                                                  │ DeliveryTracking.Api │
   │  (Port 5001)         │                                                                  │ (Port 5003)          │
   │  · Master Vendor     │                                                                  │  · Delivery Tracking │
   │  · Dynamic Auth/Map  │                                                                  │  · Live Fleet Pos    │
   │  · Geofence Master   │                                                                  │  · Redis-First (<1ms)│
   └──────────┬───────────┘                                                                  │  · Route Replay      │
              │                                                                              │  · Idempotency Guard │
              ▼                                                                              └──────────┬───────────┘
   ┌──────────────────────┐                                                                             │
   │ PostgreSQL (edcl)    │                                                                             ▼
   │ Master & Config      │                                                                  ┌──────────────────────┐
   └──────────────────────┘                                                                  │ Redis 7.2 Cache      │
                                                                                             │ Active Key / Live    │
                                                ┌────────────────────────────────────────────┴──────────▲───────────┘
                                                │                                                       │
                                                ▼                                                       │
                                     ┌──────────────────────┐                                ┌──────────┴───────────┐
                                     │ PostgreSQL (edcl)    │                                │     GpsCollector     │
                                     │ Delivery Sessions &  │                                │ (Background Worker)  │
                                     │ Partitioned Route    │                                │ · Active-Standby HA  │
                                     └──────────────────────┘                                │ · 60s Polling Loop   │
                                                                                             │ · Dual-Write Mongo   │
                                                                                             └──────────┬───────────┘
                                                                                                        │
                                                ┌───────────────────────────────────────────────────────┼────────────────────────────────────────┐
                                                ▼                                                       ▼                                        ▼
                                     ┌──────────────────────┐                                ┌──────────────────────┐                 ┌──────────────────────┐
                                     │ PostgreSQL 16        │                                │ MongoDB 7 (TTL)      │                 │ RabbitMQ 3.13        │
                                     │ Partitioned History  │                                │ · Error Log (90 hari)│                 │ · Topic Exchange     │
                                     │ (Active Jobs Only)   │                                │ · Raw Resp (30 hari) │                 │ · DLX / DLQ Retry    │
                                     └──────────────────────┘                                └──────────────────────┘                 └──────────────────────┘
```

---

## 🌟 Fitur Utama Sistem

### 1. Dynamic Response Field Mapping (`m_gps_vendor_mapping`)
Sistem tidak mengikat skema DTO secara kaku ke satu vendor. Melalui tabel master `edcl.m_gps_vendor_mapping`, nama field respons JSON vendor (misalnya `lat`, `latitude`, `y_pos`) dipetakan secara dinamis pada *runtime* ke field standar EDCL (`Y`), dan longitude ke (`X`) menggunakan *compiled expression tree* berkecapatan tinggi.

### 2. Multi-Protocol Vendor Authentication & Self-Healing Token Rotation
Mendukung strategi autentikasi vendor pihak ketiga secara otonom:
- **NoAuth / Public Key Parameter**
- **Bearer Token / Dynamic JWT** (dilengkapi rotasi token otomatis berkala via `TokenPath`)
- **Basic Authentication** (kredensial terenkripsi)
- **API Key / Custom Header Param**

### 3. Dual-Tier Storage Architecture (PostgreSQL 16 + MongoDB 7)
- **PostgreSQL 16 (Pure Delivery-Centric Storage Model)**: Riwayat spasial terstruktur (`t_delivery_latest_position`, `t_delivery_position_history`) hanya mencatat armada dengan sesi pengiriman aktif dari klik "Start Job" di `EDCLWEBAPI`. Riwayat rute disimpan pada partisi rentang bulanan (*monthly range partitioning*) yang diindeks B-Tree & BRIN.
- **MongoDB 7 (TTL Automated Cleanup)**:
  - `gps_api_logs`: Seluruh pesan galat dan HTTP status error disimpan dalam BSON dengan masa kedaluwarsa otomatis 90 hari.
  - `gps_raw_responses`: Payload JSON mentah dari vendor disimpan untuk keperluan audit dan replay jika mapping berubah (TTL 30 hari).

### 4. Redis High-Speed Position Caching & Distributed Leader Election
- **Redis-First Read Engine**: Endpoint kueri posisi armada pada `DeliveryTracking.Api` mengecek memori Redis terlebih dahulu (latensi < 1ms) sebelum melakukan *fallback* ke PostgreSQL.
- **Worker Active-Standby Leader Election**: Worker `GpsCollector` menerapkan *distributed lock* (`SET NX EX`) melalui Redis. Hanya 1 instance yang aktif melakukan polling, sementara instance standby siap mengambil alih dalam < 90 detik jika terjadi kegagalan (*failover*).

### 5. Event-Driven Messaging (RabbitMQ with DLX/DLQ)
Setiap paket koordinat yang divalidasi langsung diterbitkan ke `topic_exchange` dengan *routing key* berbasis vendor (`gps.vendor.<vendor_code>`). Dilengkapi dengan *Dead Letter Exchange* (`edcl.telematics.dlx`) dan *Retry Queue* untuk menjamin keandalan pengiriman pesan 100% tanpa risiko kehilangan data.

### 6. End-to-End Tracing & Correlation Standardization
Menerapkan standar industri terpadu untuk pelacakan alur data dan logging di seluruh microservices:
- **HTTP Response Headers**:
  ```http
  X-Trace-Id: 4bf92f3577b34da6a3ce929d0e0e4736
  X-Correlation-Id: req-client-abc-12345
  ```
- **JSON Response Body (Sukses & Error ProblemDetails)**:
  ```json
  {
    "data": { ... },
    "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
    "correlationId": "req-client-abc-12345"
  }
  ```

### 7. Enterprise Idempotency Protection (`X-Idempotency-Key`)
Mencegah mutasi ganda (*double-click* atau *network retry*) pada endpoint transaksi penting (`POST /api/v1/RDeliveryOnProgress`, `POST /geofencemaster`):
- **Wajib (*Required*)**: Client wajib mengirimkan header `X-Idempotency-Key: <UUID>`. Jika tidak ada, request ditolak langsung dengan status `400 Bad Request`.
- **Anti-Double Request Simultan**: Jika request sedang berjalan, request paralel kedua langsung mendapat status `409 Conflict`.
- **Payload Hash Guard**: Memverifikasi SHA-256 payload body. Jika key dipakai ulang untuk isi yang berbeda, sistem mengembalikan status `422 Unprocessable Entity`.
- **Instant Cached Replay**: Request ulang dengan payload yang sama langsung dikembalikan dari cache Redis (`X-Cache: HIT`, `X-Idempotent-Replayed: true`) tanpa query ulang ke database.

### 8. Full Observability (Prometheus & Grafana)
Seluruh microservice mengekspos endpoint metrik standar OpenTelemetry di `/metrics` yang di-scrape secara otomatis oleh **Prometheus** (Port 9090) dan divisualisasikan melalui **Grafana** (Port 3000) dengan metrik performa khusus seperti `edcl_gps_collector_records_inserted_total`, `edcl_gps_collector_cycle_duration_seconds`, dan `edcl_gps_collector_vendor_poll_status`.

---

## 🧩 Struktur Direktori Solusi

```
EDCLGPSAPI/
├── src/
│   ├── Services/
│   │   ├── ApiGateway/              # YARP Gateway, Rate Limiting, OpenTelemetry (Port 5000)
│   │   ├── GpsCollector/            # Background Ingestion Worker, MongoDB/Redis/RabbitMQ
│   │   ├── VendorMaster.Api/        # Vendor Profiles & Geofence Master CRUD (Port 5001)
│   │   └── DeliveryTracking.Api/    # Delivery Progress, Live Positions & Route Replay API (Port 5003)
│   ├── Shared/
│   │   ├── Shared/                  # Kernel, Idempotency, Telemetry, DbContext, Redis/Mongo
│   │   ├── Shared.Contracts/        # Integration Events (GpsLastPositionHDto)
│   │   ├── Shared.Messaging/        # Messaging Contracts
│   │   └── Shared.Messaging.RabbitMqClient/ # RabbitMQ Publisher & Consumer Extensions
│   ├── docker-compose.yml           # Multi-container Compose (Services + Infra + Monitoring)
│   └── edcl-gps-api.sln             # Visual Studio / .NET Solution File
├── database/                        # Skrip DDL PostgreSQL, MongoDB init, Prometheus & RabbitMQ config
├── deploy/
│   ├── k8s/                         # Kubernetes Manifests (k3d / Minikube / EKS)
│   └── aws/                         # Panduan & Konfigurasi AWS EKS Production Deployment
├── ARCHITECTURE.md                  # Cetak Biru Arsitektur Lengkap
├── DECOMMISSIONING_PLAN.md          # Dokumentasi Dekomposisi & Audit Trail
└── MICROSERVICES_PLAN.md            # Rencana Dekomposisi & Strategi Database
```

---

## 🚀 Panduan Menjalankan Sistem (Local Development)

### 1. Prasyarat Sistem
- **Docker & Docker Compose** (v2.20+)
- **.NET 8 SDK / .NET 9 SDK** (jika menjalankan langsung via CLI)

---

### 2. Menjalankan Seluruh Stack via Docker Compose (Rekomendasi)

Jalankan perintah berikut di root folder proyek:

```bash
docker compose -f src/docker-compose.yml up -d --build
```

Seluruh layanan aplikasi, infrastruktur database, message broker, dan monitoring akan aktif secara otomatis:

| Layanan / Komponen | Port Host | Deskripsi / URL Akses |
|---|---|---|
| **YARP API Gateway** | **`5000`** | Pintu gerbang utama REST API: `http://localhost:5000/` |
| **Vendor Master API** | **`5001`** | `http://localhost:5001/swagger` (Metrik: `/metrics`) |
| **Delivery Tracking API** | **`5003`** | `http://localhost:5003/swagger` (Live Positions & Routes, Metrik: `/metrics`) |
| **GPS Collector Worker** | — | Background ingestion daemon (Metrik internal: `/metrics`) |
| **Prometheus Server** | **`9090`** | Dashboard scrape metrik: `http://localhost:9090/` |
| **Grafana Dashboard** | **`3000`** | Visualisasi Telemetri: `http://localhost:3000/` *(User: `admin`, Pass: `admin`)* |
| **PostgreSQL Database** | **`5436`** | Port 5436 *(User: `edcl_gps_user`, DB: `AE031_EDCL_GPS_DB`)* |
| **MongoDB NoSQL** | **`27017`** | Port 27017 *(DB: `edcl_logs_db`)* |
| **Redis Cache** | **`6379`** | Port 6379 |
| **RabbitMQ Management** | **`15672`** | `http://localhost:15672/` *(User: `guest`, Pass: `guest`)* |

---

### 3. Menjalankan Layanan Secara Terpisah via .NET CLI

Jika ingin melakukan *debugging* langsung pada satu atau beberapa layanan tertentu:

```bash
# 1. Jalankan API Gateway
dotnet run --project src/Services/ApiGateway

# 2. Jalankan Vendor & Geofence Master API
dotnet run --project src/Services/VendorMaster.Api

# 3. Jalankan Delivery Tracking API (termasuk Live Position & Route Tracking)
dotnet run --project src/Services/DeliveryTracking.Api

# 4. Jalankan Background Collector Worker
dotnet run --project src/Services/GpsCollector
```

---

## 🔒 Tata Kelola Keamanan & Kepatuhan Industri
- **Enkripsi Kredensial**: Seluruh kata sandi vendor dan token API disimpan dalam bentuk terenkripsi atau dikelola via AWS Secrets Manager saat berjalan di Kubernetes / Fargate.
- **TLS/SSL Encryption in Transit**: Pertukaran pesan antara AWS Cloud dan TMMIN Private Network diwajibkan menggunakan protokol `AMQPS` (TLS 1.3) dan saluran VPN terisolasi.
- **Audit Kemampuan Telusur (*Traceability*)**: Pelacakan end-to-end terhubung melalui W3C `X-Trace-Id` dan `X-Correlation-Id` pada setiap log dan respons API.
- **Anti-Duplikasi Data**: Perlindungan `X-Idempotency-Key` menjamin tidak terjadi mutasi ganda data logistik penting pada kondisi jaringan terputus-putus.

---

<div align="center">

**EDCL GPS Telematics Platform** — *Sub-system of EDCL-Web-API, Toyota Motor Manufacturing Indonesia (TMMIN) Logistics Ecosystem.*

</div>
