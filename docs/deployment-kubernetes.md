# Kubernetes Deployment Guide

Deploy OmniOps API to Kubernetes with Helm for reproducible, zero-downtime rolling updates.

## Architecture

```
Internet → Ingress (nginx) → Service → Deployment (API pods)
                                              ↓
                         PostgreSQL / Redis / Kafka (external or in-cluster)
```

## Prerequisites

| Component | Version | Notes |
|-----------|---------|-------|
| Kubernetes | 1.28+ | Any managed cluster (AKS, EKS, GKE) or local (kind, minikube) |
| Helm | 3.14+ | Chart at `infra/helm/omniops` |
| Ingress controller | nginx recommended | For external traffic |
| Metrics Server | required for HPA | `kubectl top pods` should work |
| Container registry | GHCR or other | Image built via `.github/workflows/docker.yml` |

## Quick start (local cluster)

### 1. Build the image

```bash
docker build -f backend/Dockerfile -t omniops-api:local .
```

### 2. Load into kind (if using kind)

```bash
kind load docker-image omniops-api:local
```

### 3. Install with Helm

```bash
helm upgrade --install omniops infra/helm/omniops \
  --namespace omniops \
  --create-namespace \
  --set image.repository=omniops-api \
  --set image.tag=local \
  --set image.pullPolicy=IfNotPresent \
  --set env.jwtRequireAuthentication=false \
  --set ingress.hosts[0].host=omniops.local
```

### 4. Verify

```bash
kubectl -n omniops get pods
kubectl -n omniops port-forward svc/omniops 8080:80
curl http://localhost:8080/health/ready
```

## Docker Compose (local full stack)

For development without Kubernetes:

```bash
docker compose -f infra/docker-compose.yml up --build
```

API: `http://localhost:5031`  
Health: `http://localhost:5031/health/ready`

## Production deployment

### 1. Push image via CI

On merge to `main`, GitHub Actions builds and pushes to:

```
ghcr.io/<owner>/omniops-api:latest
ghcr.io/<owner>/omniops-api:<git-sha>
```

Tagged releases (`v1.0.0`) also publish semver tags.

### 2. Configure secrets

Never commit production secrets. Use one of:

- `helm upgrade --set secrets.databasePassword=...`
- Kubernetes External Secrets Operator
- Sealed Secrets
- Cloud secret manager (Azure Key Vault, AWS Secrets Manager)

### 3. Deploy

```bash
helm upgrade --install omniops infra/helm/omniops \
  --namespace omniops \
  --create-namespace \
  --set image.repository=ghcr.io/<owner>/omniops-api \
  --set image.tag=v1.0.0 \
  --set config.databaseHost=postgres.production.svc.cluster.local \
  --set config.kafkaBootstrapServers=kafka.production.svc.cluster.local:9092 \
  --set ingress.hosts[0].host=api.omniops.example.com \
  --set secrets.databasePassword="$DB_PASSWORD" \
  --set secrets.jwtSecret="$JWT_SECRET"
```

### 4. CD workflow

The `.github/workflows/cd.yml` pipeline:

- Packages the Helm chart as an artifact
- Renders Kubernetes manifests for review
- Optionally runs `helm upgrade --install` when `KUBE_CONFIG` secret is configured

Trigger manually: **Actions → CD → Run workflow**

## Rolling updates & zero downtime

The Deployment uses:

- `strategy.rollingUpdate.maxUnavailable: 0` — always keep capacity during rollout
- `strategy.rollingUpdate.maxSurge: 1` — spin up new pod before terminating old
- Readiness probe on `/health/ready` — traffic only routes to ready pods
- `terminationGracePeriodSeconds: 30` — graceful shutdown window

## Autoscaling

HPA scales between 2–10 replicas based on CPU (70%) and memory (80%) when `autoscaling.enabled=true`.

## Pod disruption budgets

PDB ensures `minAvailable: 1` during voluntary disruptions (node upgrades, cluster autoscaler).

## Service mesh readiness

The chart uses standard Kubernetes primitives compatible with Istio/Linkerd:

- Named ports (`http`)
- Liveness/readiness HTTP probes
- ServiceAccount for mTLS identity binding

Add mesh annotations to `deployment.yaml` templates when adopting a mesh.

## CI/CD summary

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `ci.yml` | PR / push to main | .NET build, tests, NuGet cache |
| `docker.yml` | PR / push / tags | Image build, GHA cache, Trivy scan, GHCR publish |
| `cd.yml` | Tags / manual | Helm package, manifest render, optional cluster deploy |

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Pod `CrashLoopBackOff` | `kubectl logs -n omniops deploy/omniops` — usually DB/Kafka connection |
| Readiness probe failing | Verify Postgres/Redis health endpoints in app config |
| Image pull error | Set `imagePullSecrets` if using private registry |
| HPA not scaling | Install metrics-server: `kubectl get apiservice v1beta1.metrics.k8s.io` |

## Related

- Local setup: [DEV-SETUP.md](../DEV-SETUP.md)
- Docker Compose: [infra/docker-compose.yml](../infra/docker-compose.yml)
- Helm values: [infra/helm/omniops/values.yaml](../infra/helm/omniops/values.yaml)
