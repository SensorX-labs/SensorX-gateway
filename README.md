# SensorX Gateway

**SensorX Gateway** là một **API Gateway kiêm Identity Provider** được xây dựng trên ASP.NET Core 9. Hệ thống đảm nhận xác thực/phân quyền tập trung (JWT RS256), proxy request đến các downstream service thông qua YARP, và cung cấp toàn bộ observability stack (Prometheus + Grafana + OpenTelemetry).

---

## Mục lục

- [Tổng quan kiến trúc](#tổng-quan-kiến-trúc)
- [Tính năng](#tính-năng)
- [Cấu trúc dự án](#cấu-trúc-dự-án)
- [Yêu cầu](#yêu-cầu)
- [Cài đặt và chạy](#cài-đặt-và-chạy)
- [Cấu hình](#cấu-hình)
- [API Endpoints](#api-endpoints)
- [Reverse Proxy (YARP)](#reverse-proxy-yarp)
- [Bảo mật](#bảo-mật)
- [Observability](#observability)
- [Kiểm thử](#kiểm-thử)

---

## Tổng quan kiến trúc

```
Client
  │
  ▼
[Nginx]  (tùy chọn, production)
  │
  ▼
SensorX Gateway  :5053
  ├── Identity Provider  →  POST /auth/*
  │       ├── PostgreSQL  (users, roles, tokens)
  │       └── Redis       (blacklist, permissions, idempotency)
  │
  └── YARP Reverse Proxy
          ├── /api/orders/**      →  order-service:8080
          ├── /api/products/**    →  product-service:8080
          └── /api/inventory/**   →  inventory-service:8080
```

---

## Tính năng

### Identity & Auth
- **Đăng ký / Đăng nhập** với bcrypt password hash
- **JWT RS256** — access token ký bằng RSA private key, verify bằng public key
- **Refresh token** — lưu DB, single-use rolling rotation
- **Token revocation** — blacklist JTI trong Redis, hỗ trợ revoke toàn bộ token của user
- **MFA (TOTP)** — xác thực 2 bước qua Google Authenticator / Authy
- **Brute-force protection** — khóa tài khoản theo lũy thừa 2 sau N lần thất bại
- **Idempotency key** — đảm bảo register không bị duplicate khi retry
- **OpenID Connect Discovery** — endpoint `.well-known/openid-configuration`
- **JWKS endpoint** — public key cho downstream service tự verify token

### Authorization
- **RBAC + Permission động** — permissions lưu Redis, policy dạng `Permission:read:orders,write:orders`
- **Token introspect / revoke** — tương thích OAuth 2.0

### Reverse Proxy
- **YARP** forward request đến downstream service sau khi auth pass
- **Tự động inject headers** `X-User-Id`, `X-User-Roles`, `X-Correlation-Id` vào request xuôi

### Bảo mật & Middleware
- Security headers: HSTS, CSP, X-Frame-Options, X-Content-Type-Options
- CORS cấu hình theo môi trường
- Audit log bất đồng bộ (background service), tự động PII sanitize
- Correlation ID xuyên suốt request

### Observability
- **Serilog** — structured JSON logging
- **OpenTelemetry** — distributed tracing (OTLP → Grafana Tempo)
- **Prometheus** — metrics scrape tại `/metrics`
- **Grafana** — dashboard provisioning sẵn
- **Health check** — `/health` kiểm tra PostgreSQL + self

---

## Cấu trúc dự án

```
SensorX-gateway/
├── src/
│   ├── SensorX.Gateway.Api/          # ASP.NET Core entry point
│   │   ├── Controllers/              # AuthController, TokenController, WellKnownController
│   │   ├── Middleware/               # Exception, SecurityHeaders, CorrelationId, Audit
│   │   ├── Authorization/            # PermissionPolicyProvider, PermissionAuthorizationHandler
│   │   ├── ReverseProxy/             # YarpTransformProvider (inject headers)
│   │   ├── HealthChecks/
│   │   └── Program.cs
│   ├── SensorX.Gateway.Application/  # Interfaces, DTOs
│   ├── SensorX.Gateway.Domain/       # Entities, Domain interfaces
│   ├── SensorX.Gateway.Infrastructure/ # EF Core, Redis, tất cả services
│   ├── SensorX.Gateway.Test/         # xUnit integration tests
│   └── SensorX.KeyGen/               # CLI tool tạo RSA key pair
├── monitoring/
│   ├── prometheus.yml
│   └── grafana/provisioning/
├── docker-compose.yml
└── Gateway.sln
```

---

## Yêu cầu

| Công cụ | Phiên bản tối thiểu |
|---|---|
| .NET SDK | 9.0 |
| Docker & Docker Compose | 24+ |
| PostgreSQL | 16 (hoặc dùng Docker) |
| Redis | 7 (hoặc dùng Docker) |

---

## Cài đặt và chạy

### 1. Tạo RSA key pair

```bash
dotnet run --project src/SensorX.KeyGen -- src/SensorX.Gateway.Api/Keys
```

Lệnh này tạo `Keys/private.key` và `Keys/public.key`. **Không commit `private.key` lên source control.**

### 2. Chạy toàn bộ stack với Docker Compose

```bash
docker-compose up -d
```

Bao gồm: PostgreSQL, Redis, Prometheus, Grafana.

### 3. Chạy Gateway

```bash
cd src/SensorX.Gateway.Api
dotnet run
```

Gateway lắng nghe tại `http://localhost:5053`.

### 4. Swagger UI (Development)

Truy cập `http://localhost:5053/swagger` để xem và thử API.

---

## Cấu hình

File cấu hình chính: `src/SensorX.Gateway.Api/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=sensorx_gateway;Username=postgres;Password=postgres"
  },
  "JwtSettings": {
    "Issuer": "https://gateway.yourdomain.com",
    "Audience": "api",
    "AccessTokenMinutes": 15,
    "PrivateKeyPath": "Keys/private.key",
    "Kid": "key-2026-02",
    "HmacSecret": "CHANGE-THIS-IN-PRODUCTION"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Security": {
    "MaxLoginAttempts": 5
  },
  "Cors": {
    "AllowedOrigins": ["https://app.yourdomain.com"]
  },
  "ReverseProxy": { ... }
}
```

**Production:** dùng `appsettings.Production.json` và Docker Secrets cho `PrivateKeyPath`, `HmacSecret`.

---

## API Endpoints

### Auth (`/auth`)

| Method | Endpoint | Mô tả | Auth |
|---|---|---|---|
| `POST` | `/auth/register` | Đăng ký tài khoản mới | Không |
| `POST` | `/auth/login` | Đăng nhập, trả về access + refresh token | Không |
| `POST` | `/auth/refresh` | Làm mới access token | Không |
| `POST` | `/auth/logout` | Đăng xuất, blacklist token | Bearer |
| `POST` | `/auth/mfa` | Xác thực TOTP (bước 2) | Không |
| `POST` | `/auth/introspect` | Kiểm tra token còn hợp lệ không | Bearer |
| `POST` | `/auth/revoke` | Thu hồi toàn bộ token của user | Bearer |

### Token Service (`/token`)

| Method | Endpoint | Mô tả |
|---|---|---|
| `POST` | `/token` | Client credentials grant cho service-to-service |

### Well-Known

| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/.well-known/openid-configuration` | OpenID Connect Discovery Document |
| `GET` | `/.well-known/jwks.json` | Public key dạng JWKS |

### System

| Endpoint | Mô tả |
|---|---|
| `GET /health` | Health check (PostgreSQL + self) |
| `GET /metrics` | Prometheus metrics scrape |

---

## Reverse Proxy (YARP)

YARP đọc cấu hình từ section `ReverseProxy` trong `appsettings.json`:

```json
"ReverseProxy": {
  "Routes": {
    "order-route":     { "ClusterId": "order-cluster",     "Match": { "Path": "/api/orders/{**catch-all}" } },
    "product-route":   { "ClusterId": "product-cluster",   "Match": { "Path": "/api/products/{**catch-all}" } },
    "inventory-route": { "ClusterId": "inventory-cluster", "Match": { "Path": "/api/inventory/{**catch-all}" } }
  },
  "Clusters": {
    "order-cluster":     { "Destinations": { "order-service":     { "Address": "http://order-service:8080/" } } },
    "product-cluster":   { "Destinations": { "product-service":   { "Address": "http://product-service:8080/" } } },
    "inventory-cluster": { "Destinations": { "inventory-service": { "Address": "http://inventory-service:8080/" } } }
  }
}
```

Với mỗi request được forward, gateway tự động gắn:

- `X-User-Id` — ID của user đang đăng nhập (từ `sub` claim)
- `X-User-Roles` — Role của user (từ `role` claim)
- `X-Correlation-Id` — Trace ID xuyên suốt chuỗi request

---

## Bảo mật

### JWT RS256

- Gateway ký access token bằng **RSA-2048 private key**
- Downstream service verify bằng **public key** (lấy từ JWKS endpoint hoặc file)
- Không cần downstream service biết private key

### Revocation

- Access token bị revoke: JTI được lưu vào Redis với TTL = thời gian còn lại của token
- Refresh token: lưu DB, bị xóa khi logout hoặc revoke all

### Brute-force Protection

- Sau `MaxLoginAttempts` lần sai mật khẩu → khóa tài khoản
- Thời gian khóa tăng theo: `15min × 2^(số lần khóa)`

### Key Rotation

Sử dụng `SensorX.KeyGen` để tạo key mới, đăng ký vào bảng `signing_keys` trong DB. Gateway hỗ trợ nhiều key đồng thời, chọn key qua `kid` header trong JWT.

---

## Observability

| Thành phần | URL | Mô tả |
|---|---|---|
| Prometheus | `http://localhost:9090` | Thu thập metrics |
| Grafana | `http://localhost:3000` | Dashboard (admin/admin) |
| Gateway metrics | `http://localhost:5053/metrics` | Prometheus scrape endpoint |

Grafana được provisioning tự động với datasource Prometheus. Import dashboard từ `monitoring/grafana/provisioning/`.

Để bật **distributed tracing** (Grafana Tempo), cấu hình:

```json
"OpenTelemetry": {
  "OtlpEndpoint": "http://tempo:4317"
}
```

---

## Kiểm thử

```bash
dotnet test src/SensorX.Gateway.Test
```

Test suite dùng **xUnit** + **FluentAssertions**, bao gồm integration test cho các controller và service chính.

---

---

## SSL tự động & Subdomain (Traefik)

Hệ thống tích hợp **Traefik v3** làm edge router, tự động cấp và gia hạn chứng chỉ SSL qua **Let's Encrypt** mà không cần cấu hình manual.

### Subdomain layout

| Subdomain | Service | Quyền truy cập |
|---|---|---|
| `api.yourdomain.com` | SensorX Gateway | Public |
| `grafana.yourdomain.com` | Grafana | Public (auth Grafana) |
| `traefik.yourdomain.com` | Traefik Dashboard | Internal + Basic Auth |
| `prometheus.yourdomain.com` | Prometheus | Internal + Basic Auth |

### Cách hoạt động

```
Internet
   │  :80/:443
   ▼
Traefik (edge router)
   ├── Let's Encrypt ACME TLS-ALPN-01 → cấp cert tự động
   ├── HTTP → HTTPS redirect
   ├── api.domain.com:443     → gateway:8080   (rate-limited)
   ├── grafana.domain.com:443 → grafana:3000
   ├── prometheus.domain.com  → prometheus:9090 (IP whitelist + basic auth)
   └── traefik.domain.com     → dashboard       (basic auth)
```

Traefik đọc cấu hình từ **Docker labels** trực tiếp — không cần reload khi thêm service mới.

### Deploy production với Traefik

**1. Cấu hình DNS** — Tạo A record cho tất cả subdomain trỏ về IP server:
```
api.yourdomain.com        A  <SERVER_IP>
grafana.yourdomain.com    A  <SERVER_IP>
traefik.yourdomain.com    A  <SERVER_IP>
prometheus.yourdomain.com A  <SERVER_IP>
```

**2. Tạo RSA key** (nếu chưa có):
```bash
dotnet run --project src/SensorX.KeyGen -- src/SensorX.Gateway.Api/Keys
```

**3. Tạo file `.env`** từ template:
```bash
cp .env.example .env
```
Điền `DOMAIN`, `ACME_EMAIL`, password hash (xem hướng dẫn trong file).

Tạo password hash cho Traefik dashboard:
```bash
echo $(htpasswd -nb admin yourpassword) | sed -e 's/\$/\$\$/g'
```

**4. Khởi động:**
```bash
docker compose up -d
```

Traefik sẽ tự động:
- Cấp SSL cho tất cả subdomain ngay lần đầu khởi động
- Tự gia hạn cert trước khi hết hạn 30 ngày
- Forward request đến đúng service

### Phương án thay thế: Nginx + Certbot

Dùng `docker-compose.nginx.yml` nếu bạn muốn kiểm soát Nginx trực tiếp.

**Lần đầu — issue certs:**
```bash
# Khởi động nginx tạm với port 80 để Certbot xác thực HTTP-01
docker compose -f docker-compose.nginx.yml run --rm certbot-init
```

**Chạy bình thường:**
```bash
docker compose -f docker-compose.nginx.yml up -d
```

**Gia hạn cert (đặt cron job, chạy hàng tuần):**
```bash
0 0 * * 0 /path/to/nginx/ssl-renew.sh
```

Config nginx nằm tại [nginx/conf.d/default.conf](nginx/conf.d/default.conf) — thay `yourdomain.com` bằng domain thực tế.

### Cấu trúc file mới

```
SensorX-gateway/
├── Dockerfile                    # Multi-stage build cho gateway
├── docker-compose.yml            # Production stack (Traefik)
├── docker-compose.dev.yml        # Dev override (no Traefik, direct ports)
├── docker-compose.nginx.yml      # Alternative: Nginx + Certbot
├── .env.example                  # Template biến môi trường
├── traefik/
│   ├── traefik.yml               # Static config (entrypoints, ACME, providers)
│   └── dynamic/
│       ├── middlewares.yml       # Rate limit, auth, security headers
│       └── tls.yml               # TLS options (cipher suites)
└── nginx/
    ├── nginx.conf                # Nginx main config
    ├── conf.d/default.conf       # Virtual hosts + SSL
    └── ssl-renew.sh              # Cert renewal script
```

---

## Local Development

```bash
# Chạy infrastructure (DB, Redis, Prometheus, Grafana) + gateway (dev mode)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d

# Gateway chạy tại http://localhost:5053
# Swagger UI: http://localhost:5053/swagger
# Grafana:    http://localhost:3000
# Prometheus: http://localhost:9090
```

---

## License

MIT
