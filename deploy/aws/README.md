# ☁️ AWS EKS Production Deployment Guide — EDCL GPS Telematics

Panduan ini menjelaskan langkah-langkah *zero-downtime deployment* dan *environment parity* untuk menjalankan microservices EDCL GPS Telematics di **AWS EKS** (Region Singapore: `ap-southeast-1`).

---

## 🏗️ 1. Arsitektur AWS Managed Services

| Komponen Lokal | AWS Managed Equivalent | Catatan |
|---|---|---|
| Kubernetes (k3d) | **Amazon EKS (v1.30)** | Multi-AZ Managed Nodes + IRSA + Spot AutoScaler |
| PostgreSQL 16 | **Amazon RDS PostgreSQL** | Multi-AZ, db.t3.medium, Range Partitions |
| MongoDB 7 | **MongoDB Atlas M0/M10** | Dual-write logging & Raw responses (TTL Built-in) |
| Redis 7 | **Amazon ElastiCache Redis** | Cluster Multi-AZ, Last position cache & Leader election |
| RabbitMQ | **Amazon MQ for RabbitMQ** | Multi-AZ Message Broker (AMQP 5671 TLS) |
| NGINX Ingress | **AWS ALB (Application Load Balancer)** | Terminate SSL di ACM, Target IP Pods |
| Secret Management | **AWS Secrets Manager + ESO** | Auto-rotate, Zero plaintext secret in Git |

---

## 🚀 2. Tahapan Deployment

### Langkah 1: Provisioning EKS Cluster
```bash
# Buat cluster EKS dengan eksctl
eksctl create cluster -f deploy/aws/eksctl/cluster.yaml
```

### Langkah 2: Provisioning Database & Cache via Terraform
```bash
cd deploy/aws/terraform
terraform init
terraform apply -var="vpc_id=vpc-xxxxxx" -var="subnet_ids=[\"subnet-a\",\"subnet-b\"]"
```

### Langkah 3: Setup External Secrets Operator (ESO)
```bash
# Tambahkan helm repo ESO
helm repo add external-secrets https://charts.external-secrets.io
helm install external-secrets external-secrets/external-secrets -n kube-system

# Terapkan SecretStore & ExternalSecret
kubectl apply -f deploy/k8s/aws/13-external-secrets.yaml
```

### Langkah 4: Terapkan ConfigMap & Manifests Aplikasi
```bash
# Terapkan ConfigMap dengan endpoint AWS
kubectl apply -f deploy/k8s/aws/01-configmap-aws.template.yaml

# Terapkan Deployments Microservices
kubectl apply -f deploy/k8s/07-api-gateway-deployment.yaml
kubectl apply -f deploy/k8s/08-position-query-deployment.yaml
kubectl apply -f deploy/k8s/09-vendor-master-deployment.yaml
kubectl apply -f deploy/k8s/10-delivery-tracking-deployment.yaml
kubectl apply -f deploy/k8s/05-gps-collector-deployment.yaml

# Terapkan Network Policies & Alerts
kubectl apply -f deploy/k8s/11-monitoring-alerts.yaml
kubectl apply -f deploy/k8s/12-network-policies.yaml

# Terapkan AWS ALB Ingress
kubectl apply -f deploy/k8s/aws/06-ingress-alb.yaml
```

---

## 🛡️ 3. SLA & Resiliensi
- **Uptime Target:** 99.9%
- **Worker Failover Time:** < 90 detik via Redis Lease
- **Read Latency:** < 5ms via Redis ElastiCache
