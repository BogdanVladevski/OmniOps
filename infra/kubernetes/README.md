# Kubernetes manifests

Plain Kubernetes resources for OmniOps are generated from the Helm chart.

## Recommended: Helm (primary)

```bash
helm upgrade --install omniops infra/helm/omniops \
  --namespace omniops \
  --create-namespace \
  --set image.repository=ghcr.io/<your-org>/omniops-api \
  --set image.tag=latest \
  --set secrets.databasePassword='<strong-password>' \
  --set secrets.jwtSecret='<at-least-32-chars>'
```

See [docs/deployment-kubernetes.md](../../docs/deployment-kubernetes.md) for the full guide.

## Render manifests without installing

```bash
helm template omniops infra/helm/omniops \
  --namespace omniops \
  > infra/kubernetes/rendered.yaml
```

## What is included in the chart

| Resource | Purpose |
|----------|---------|
| Namespace | Isolates OmniOps workloads |
| ConfigMap | Non-secret app configuration |
| Secret | Database credentials, JWT secret |
| Deployment | API pods with rolling updates (`maxUnavailable: 0`) |
| Service | ClusterIP on port 80 → container 8080 |
| Ingress | External HTTP routing (nginx class) |
| HorizontalPodAutoscaler | CPU/memory autoscaling (2–10 replicas) |
| PodDisruptionBudget | Keeps at least 1 pod during node drains |
| ServiceAccount | Pod identity for future RBAC |

## Prerequisites

- Kubernetes 1.28+
- Ingress controller (e.g. nginx-ingress)
- Metrics Server (for HPA)
- External PostgreSQL, Redis, and Kafka (or in-cluster operators)

The API container expects backing services reachable via the hostnames in `values.yaml` (`config.databaseHost`, etc.).
