# ☸️ Kubernetes Deployment Guide — EDCL GPS Telematics

Folder ini berisi seluruh manifest Kubernetes untuk menjalankan platform **EDCL GPS Telematics** baik di lingkungan pengujian lokal (menggunakan **k3d** / **Minikube**) maupun di **AWS EKS** dengan prinsip *100% Environment Parity*.

---

## 📁 Struktur Manifests

```
deploy/k8s/
├── 00-namespace.yaml                 # Namespace: edcl-prod, edcl-staging, edcl-infra, edcl-monitoring
├── 01-configmap.yaml                 # ConfigMap edcl-gps-config (Endpoints & App Settings)
├── 02-secret.template.yaml           # Template Kredensial Database & Broker
├── 03-infra-local.yaml               # Stack PostgreSQL, MongoDB 7, Redis, RabbitMQ untuk K8s lokal
├── 04-gps-api-deployment.yaml        # Deployment & Service (ClusterIP) untuk REST API
├── 05-gps-collector-deployment.yaml  # Deployment Background Worker (Replicas: 2, HA)
├── 06-ingress.yaml                   # Routing Ingress (NGINX / AWS ALB) ke API Gateway
├── 07-api-gateway-deployment.yaml    # YARP API Gateway 1 Pintu (Port 8080 / 80)
├── 08-position-query-deployment.yaml # High-throughput Query Service (HPA 2-10 replicas)
├── 09-vendor-master-deployment.yaml  # Vendor & Geofence Master Service (Replicas: 2)
├── 10-delivery-tracking-deployment.yaml # Delivery Tracking Service (HPA 2-5 replicas)
├── 11-monitoring-alerts.yaml         # Prometheus AlertManager Rules (Leader, Stale, Errors)
├── 12-network-policies.yaml          # Zero-Trust Network Policies (Tier Isolation)
└── README.md                         # Panduan operasional (file ini)
```

---

## 🚀 Cara Menjalankan di Kubernetes Lokal (k3d)

### 1. Buat Cluster Lokal dengan k3d
```bash
# Buat cluster dengan port forwarding Ingress ke localhost:8080
k3d cluster create edcl-dev --port "8080:80@loadbalancer" --agents 2
```

### 2. Deploy Namespace & Infrastruktur Pendukung
```bash
# Buat namespaces
kubectl apply -f deploy/k8s/00-namespace.yaml

# Jalankan PostgreSQL, MongoDB, Redis, dan RabbitMQ di namespace edcl-infra
kubectl apply -f deploy/k8s/03-infra-local.yaml
```

### 3. Deploy Konfigurasi & Secret
```bash
# Terapkan ConfigMap
kubectl apply -f deploy/k8s/01-configmap.yaml

# Salin template secret dan terapkan
cp deploy/k8s/02-secret.template.yaml deploy/k8s/02-secret.yaml
kubectl apply -f deploy/k8s/02-secret.yaml
```

### 4. Deploy Aplikasi, API Gateway & Ingress
```bash
# Deploy Backend Services
kubectl apply -f deploy/k8s/04-gps-api-deployment.yaml
kubectl apply -f deploy/k8s/05-gps-collector-deployment.yaml

# Deploy YARP API Gateway (Satu Pintu Masuk)
kubectl apply -f deploy/k8s/07-api-gateway-deployment.yaml

# Deploy Ingress
kubectl apply -f deploy/k8s/06-ingress.yaml
```

### 5. Verifikasi Status Pods
```bash
kubectl get pods -n edcl-infra
kubectl get pods -n edcl-prod
curl http://localhost:8080/
curl http://localhost:8080/hc
```

---

## ☁️ Transisi ke AWS EKS (Zero Code Change)

Ketika berpindah ke AWS EKS:
1. **Container Image**: Tetap sama persis (`ghcr.io/odealidj/edcl-gps-api:latest`).
2. **Infrastruktur Database**:
   - `03-infra-local.yaml` tidak perlu di-apply di AWS.
   - Gunakan AWS Managed Services: **Amazon RDS PostgreSQL**, **MongoDB Atlas M0/M10**, **Amazon ElastiCache Redis**, dan **Amazon MQ RabbitMQ**.
3. **Konfigurasi Hostname**:
   - Cukup update `01-configmap.yaml` dengan endpoint RDS / Atlas / ElastiCache.
4. **Ingress**:
   - Pada `06-ingress.yaml`, aktifkan anotasi `alb` untuk memicu otomatisasi pembuatan AWS Application Load Balancer.
