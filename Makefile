# ==============================================================================
# 🛰️ EDCL GPS API — Microservices Platform Automation Makefile
# ==============================================================================

SHELL := /bin/bash
export PATH := $(PATH):$(HOME)/.dotnet/tools
COMPOSE_FILES := -f src/docker-compose.yml -f src/docker-compose.override.yml
SONAR_COMPOSE := -f deploy/sonar/docker-compose.sonar.yml
SONAR_PROJECT_KEY ?= edcl-gps-telematics-api
SONAR_PROJECT_NAME ?= "EDCL GPS Telematics API"
SONAR_HOST_URL ?= http://localhost:9000
SONAR_TOKEN ?= $(token)
SOLUTION := src/edcl-gps-api.sln
PID_DIR := .local-pids
LOG_DIR := .local-logs

# ANSI Colors for Terminal Output
CYAN    := \033[36m
GREEN   := \033[32m
YELLOW  := \033[33m
RED     := \033[31m
MAGENTA := \033[35m
BOLD    := \033[1m
RESET   := \033[0m

# List of Infrastructure Services in docker-compose
INFRA_SERVICES := ae031db distributedcache messagebus5672 mongodb mongo-express prometheus grafana seq

.PHONY: help infra-up infra-down infra-restart run-all stop-all restart-all \
        run-local-all stop-local-all restart-local-all status logs-all logs-local \
        build build-aot docker-build-aot test test-delivery-sim test-happy-path clean health \
        run-gateway stop-gateway restart-gateway logs-gateway \
        run-vendor stop-vendor restart-vendor logs-vendor \
        run-position stop-position restart-position logs-position \
        run-delivery stop-delivery restart-delivery logs-delivery \
        run-collector stop-collector restart-collector logs-collector \
        run-api-gateway stop-api-gateway restart-api-gateway logs-api-gateway \
        run-vendor-master stop-vendor-master restart-vendor-master logs-vendor-master \
        run-position-query stop-position-query restart-position-query logs-position-query \
        run-delivery-tracking stop-delivery-tracking restart-delivery-tracking logs-delivery-tracking \
        run-gps-collector stop-gps-collector restart-gps-collector logs-gps-collector \
        run stop restart logs \
        docker-restart docker-logs docker-stop docker-run \
        sonar-up sonar-down sonar-restart sonar-status sonar-logs sonar-scan sonar-clean

default: help

# ------------------------------------------------------------------------------
# 📖 Help & Menus
# ------------------------------------------------------------------------------
help:
	@echo -e "${BOLD}${CYAN}========================================================================${RESET}"
	@echo -e "${BOLD}${CYAN} 🛰️  EDCL GPS TELEMATICS MICROSERVICES PLATFORM — CLI COMMANDS${RESET}"
	@echo -e "${BOLD}${CYAN}========================================================================${RESET}"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}🧱 INFRASTRUCTURE ONLY (Postgres, Mongo, Redis, RabbitMQ, Observability):${RESET}"
	@echo -e "  ${GREEN}make infra-up${RESET}              Start all infrastructure containers in Docker"
	@echo -e "  ${GREEN}make infra-down${RESET}            Stop infrastructure containers"
	@echo -e "  ${GREEN}make infra-restart${RESET}         Restart infrastructure containers"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}💻 LOCAL PC MICROSERVICES (Lean Topology: 3 Core + 1 Gateway):${RESET}"
	@echo -e "  ${GREEN}make run-local-all${RESET}         Start infra in Docker + all Lean .NET services locally"
	@echo -e "  ${GREEN}make stop-local-all${RESET}        Stop all locally running .NET services"
	@echo -e "  ${GREEN}make restart-local-all${RESET}     Restart all local .NET services"
	@echo -e "  ${GREEN}make logs-local${RESET}            Tail live logs of all local services"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}🎯 LOCAL PC INDIVIDUAL .NET SERVICES (Run / Stop / Restart / Logs):${RESET}"
	@echo -e "  ${GREEN}make run-gateway${RESET}           Start YARP ApiGateway (:5000)"
	@echo -e "  ${GREEN}make stop-gateway${RESET}          Stop YARP ApiGateway"
	@echo -e "  ${GREEN}make restart-gateway${RESET}       Restart YARP ApiGateway"
	@echo -e "  ${GREEN}make logs-gateway${RESET}          Tail logs of ApiGateway"
	@echo -e ""
	@echo -e "  ${GREEN}make run-vendor${RESET}            Start VendorMaster.Api (:5001)"
	@echo -e "  ${GREEN}make stop-vendor${RESET}           Stop VendorMaster.Api"
	@echo -e "  ${GREEN}make restart-vendor${RESET}        Restart VendorMaster.Api"
	@echo -e "  ${GREEN}make logs-vendor${RESET}           Tail logs of VendorMaster.Api"
	@echo -e ""
	@echo -e "  ${GREEN}make run-delivery${RESET}          Start DeliveryTracking.Api (:5003 - includes Live Positions)"
	@echo -e "  ${GREEN}make stop-delivery${RESET}         Stop DeliveryTracking.Api"
	@echo -e "  ${GREEN}make restart-delivery${RESET}      Restart DeliveryTracking.Api"
	@echo -e "  ${GREEN}make logs-delivery${RESET}         Tail logs of DeliveryTracking.Api"
	@echo -e ""
	@echo -e "  ${GREEN}make run-collector${RESET}         Start GpsCollector Background Worker (:5004)"
	@echo -e "  ${GREEN}make stop-collector${RESET}        Stop GpsCollector Background Worker"
	@echo -e "  ${GREEN}make restart-collector${RESET}     Restart GpsCollector Background Worker"
	@echo -e "  ${GREEN}make logs-collector${RESET}        Tail logs of GpsCollector"
	@echo -e ""
	@echo -e "  ${GREEN}make run-position${RESET}          [Deprecated] PositionQuery.Api (Consolidated into DeliveryTracking.Api)"
	@echo -e "  ${GREEN}make stop-position${RESET}         Stop PositionQuery.Api (if running)"
	@echo -e ""
	@echo -e "  ${CYAN}💡 Parameterized syntax: make run s=<svc> | make stop s=<svc> | make restart s=<svc>${RESET}"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}📦 DOCKER ALL-IN-ONE (Run Entire Platform inside Docker Compose):${RESET}"
	@echo -e "  ${GREEN}make run-all${RESET}               Build & start everything (Lean services + infra) in Docker"
	@echo -e "  ${GREEN}make stop-all${RESET}              Stop & teardown all Docker containers"
	@echo -e "  ${GREEN}make restart-all${RESET}           Restart all Docker containers"
	@echo -e "  ${GREEN}make logs-all${RESET}              Tail live logs of all Docker containers"
	@echo -e "  ${GREEN}make docker-restart s=<svc>${RESET} Restart single Docker container (e.g. s=vendor-master-svc)"
	@echo -e "  ${GREEN}make docker-logs s=<svc>${RESET}    Tail logs of single Docker container"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}📊 STATUS & MONITORING:${RESET}"
	@echo -e "  ${GREEN}make status${RESET}                Display running containers, local processes & ports"
	@echo -e "  ${GREEN}make health${RESET}                Probe /hc endpoints across all microservices"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}🛠️  BUILD & MAINTENANCE:${RESET}"
	@echo -e "  ${GREEN}make build${RESET}                 Compile the entire solution (dotnet build)"
	@echo -e "  ${GREEN}make build-aot${RESET}             Publish ApiGateway with Native AOT (dotnet publish)"
	@echo -e "  ${GREEN}make docker-build-aot${RESET}      Build ultra-slim Native AOT container (33MB chiseled)"
	@echo -e "  ${GREEN}make test${RESET}                  Run unit & integration tests (dotnet test)"
	@echo -e "  ${GREEN}make test-delivery-sim${RESET}     Simulasi happy path live delivery GPS tracking (2m breadcrumbs)"
	@echo -e "  ${GREEN}make clean${RESET}                 Clean build artifacts (bin/obj) & local log/pid files"
	@echo -e ""

	@echo -e "${BOLD}${YELLOW}🔍 SONARQUBE CODE QUALITY & STATIC ANALYSIS:${RESET}"
	@echo -e "  ${GREEN}make sonar-up${RESET}              Start SonarQube & dedicated PostgreSQL containers"
	@echo -e "  ${GREEN}make sonar-down${RESET}            Stop SonarQube containers"
	@echo -e "  ${GREEN}make sonar-restart${RESET}         Restart SonarQube containers"
	@echo -e "  ${GREEN}make sonar-status${RESET}          Check SonarQube container health & web status"
	@echo -e "  ${GREEN}make sonar-logs${RESET}            Tail live logs of SonarQube container"
	@echo -e "  ${GREEN}make sonar-scan${RESET}            Run full .NET 10 analysis & test coverage (token=<TOKEN>)"
	@echo -e "  ${GREEN}make sonar-clean${RESET}           Tear down containers & remove SonarQube data volumes"
	@echo -e ""

# ------------------------------------------------------------------------------
# 🧱 Infrastructure Only Targets
# ------------------------------------------------------------------------------
infra-up:
	@echo -e "${BOLD}${CYAN}🚀 Starting Infrastructure Stack (PostgreSQL, MongoDB, Redis, RabbitMQ, Prometheus, Grafana, Seq)...${RESET}"
	@docker compose $(COMPOSE_FILES) up -d $(INFRA_SERVICES)
	@echo -e "${BOLD}${GREEN}✅ Infrastructure services are running!${RESET}"
	@echo -e "  • PostgreSQL    : localhost:5436"
	@echo -e "  • MongoDB       : localhost:27017 (MongoExpress: http://localhost:8081)"
	@echo -e "  • Redis         : localhost:6379"
	@echo -e "  • RabbitMQ      : localhost:5672  (Management:   http://localhost:15672)"
	@echo -e "  • Prometheus    : http://localhost:9090"
	@echo -e "  • Grafana       : http://localhost:3000 (admin / admin)"
	@echo -e "  • Seq Logs      : http://localhost:5341"

infra-down:
	@echo -e "${BOLD}${YELLOW}🛑 Stopping Infrastructure Containers...${RESET}"
	@docker compose $(COMPOSE_FILES) stop $(INFRA_SERVICES)
	@echo -e "${BOLD}${GREEN}✅ Infrastructure containers stopped.${RESET}"

infra-restart: infra-down infra-up

# ------------------------------------------------------------------------------
# 📦 Docker Targets
# ------------------------------------------------------------------------------
run-all:
	@echo -e "${BOLD}${CYAN}🚀 Building and Starting Entire Platform in Docker Compose...${RESET}"
	@docker compose $(COMPOSE_FILES) up -d --build
	@echo -e "${BOLD}${GREEN}✅ All microservices & infrastructure are running in Docker!${RESET}"
	@echo -e "  • YARP API Gateway  : http://localhost:5000 (or :8080)"
	@echo -e "  • VendorMaster API  : http://localhost:5001/swagger"
	@echo -e "  • DeliveryTrack API : http://localhost:5003/swagger (Live Positions & Tracking)"
	@echo -e "  • Prometheus        : http://localhost:9090"
	@echo -e "  • Grafana           : http://localhost:3000"

stop-all:
	@echo -e "${BOLD}${YELLOW}🛑 Tearing down all Docker Compose containers...${RESET}"
	@docker compose $(COMPOSE_FILES) down
	@echo -e "${BOLD}${GREEN}✅ All Docker containers stopped and removed.${RESET}"

restart-all: stop-all run-all

logs-all:
	@docker compose $(COMPOSE_FILES) logs -f

docker-restart:
	@if [ -z "$(s)" ]; then \
		echo -e "${RED}Error: Service name required. Example: make docker-restart s=vendor-master-svc${RESET}"; \
	else \
		docker compose $(COMPOSE_FILES) restart $(s); \
	fi

docker-logs:
	@if [ -z "$(s)" ]; then \
		echo -e "${RED}Error: Service name required. Example: make docker-logs s=vendor-master-svc${RESET}"; \
	else \
		docker compose $(COMPOSE_FILES) logs -f $(s); \
	fi

docker-stop:
	@if [ -z "$(s)" ]; then \
		echo -e "${RED}Error: Service name required. Example: make docker-stop s=vendor-master-svc${RESET}"; \
	else \
		docker compose $(COMPOSE_FILES) stop $(s); \
	fi

docker-run:
	@if [ -z "$(s)" ]; then \
		echo -e "${RED}Error: Service name required. Example: make docker-run s=vendor-master-svc${RESET}"; \
	else \
		docker compose $(COMPOSE_FILES) up -d $(s); \
	fi

# ------------------------------------------------------------------------------
# 💻 Local PC Microservices — Individual .NET Targets
# ------------------------------------------------------------------------------

# --- 1. YARP API Gateway (:5000) ---
run-gateway:
	@mkdir -p $(PID_DIR) $(LOG_DIR)
	@if [ -f "$(PID_DIR)/api-gateway.pid" ] && kill -0 $$(cat $(PID_DIR)/api-gateway.pid) 2>/dev/null; then \
		echo -e "  ${YELLOW}⚠️  ApiGateway is already running (PID: $$(cat $(PID_DIR)/api-gateway.pid))${RESET}"; \
	else \
		echo -e "  ${GREEN}▶ Starting ApiGateway (:5000)...${RESET}"; \
		setsid nohup dotnet run --no-build --project src/Services/ApiGateway --launch-profile http < /dev/null > $(LOG_DIR)/api-gateway.log 2>&1 & \
		PID=$$!; \
		echo $$PID > $(PID_DIR)/api-gateway.pid; \
		sleep 1.5; \
		if kill -0 $$PID 2>/dev/null; then \
			echo -e "  ${GREEN}✅ ApiGateway started (PID: $$PID) at http://localhost:5000${RESET}"; \
		else \
			echo -e "  ${RED}❌ Failed to start ApiGateway! Check log: $(LOG_DIR)/api-gateway.log${RESET}"; \
		fi; \
	fi

stop-gateway:
	@echo -e "  ${YELLOW}🛑 Stopping ApiGateway...${RESET}"
	@if [ -f "$(PID_DIR)/api-gateway.pid" ]; then \
		PID=$$(cat $(PID_DIR)/api-gateway.pid); \
		if kill -0 $$PID 2>/dev/null; then \
			kill -TERM $$PID 2>/dev/null || true; \
			for i in {1..5}; do kill -0 $$PID 2>/dev/null || break; sleep 0.2; done; \
			kill -9 $$PID 2>/dev/null || true; \
		fi; \
		rm -f $(PID_DIR)/api-gateway.pid; \
	fi
	@pkill -f 'ApiGateway\.dll' 2>/dev/null || true
	@echo -e "  ${GREEN}✅ ApiGateway stopped.${RESET}"

restart-gateway: stop-gateway run-gateway

logs-gateway:
	@tail -n 50 -f $(LOG_DIR)/api-gateway.log 2>/dev/null || echo -e "${YELLOW}No log file found for ApiGateway.${RESET}"

# Aliases for Gateway
run-api-gateway: run-gateway
stop-api-gateway: stop-gateway
restart-api-gateway: restart-gateway
logs-api-gateway: logs-gateway


# --- 2. VendorMaster.Api (:5001) ---
run-vendor:
	@mkdir -p $(PID_DIR) $(LOG_DIR)
	@if [ -f "$(PID_DIR)/vendor-master.pid" ] && kill -0 $$(cat $(PID_DIR)/vendor-master.pid) 2>/dev/null; then \
		echo -e "  ${YELLOW}⚠️  VendorMaster.Api is already running (PID: $$(cat $(PID_DIR)/vendor-master.pid))${RESET}"; \
	else \
		echo -e "  ${GREEN}▶ Starting VendorMaster.Api (:5001)...${RESET}"; \
		setsid nohup dotnet run --no-build --project src/Services/VendorMaster/VendorMaster.Api --launch-profile http < /dev/null > $(LOG_DIR)/vendor-master.log 2>&1 & \
		PID=$$!; \
		echo $$PID > $(PID_DIR)/vendor-master.pid; \
		sleep 1.5; \
		if kill -0 $$PID 2>/dev/null; then \
			echo -e "  ${GREEN}✅ VendorMaster.Api started (PID: $$PID) at http://localhost:5001/swagger${RESET}"; \
		else \
			echo -e "  ${RED}❌ Failed to start VendorMaster.Api! Check log: $(LOG_DIR)/vendor-master.log${RESET}"; \
		fi; \
	fi

stop-vendor:
	@echo -e "  ${YELLOW}🛑 Stopping VendorMaster.Api...${RESET}"
	@if [ -f "$(PID_DIR)/vendor-master.pid" ]; then \
		PID=$$(cat $(PID_DIR)/vendor-master.pid); \
		if kill -0 $$PID 2>/dev/null; then \
			kill -TERM $$PID 2>/dev/null || true; \
			for i in {1..5}; do kill -0 $$PID 2>/dev/null || break; sleep 0.2; done; \
			kill -9 $$PID 2>/dev/null || true; \
		fi; \
		rm -f $(PID_DIR)/vendor-master.pid; \
	fi
	@pkill -f 'VendorMaster\.Api\.dll' 2>/dev/null || true
	@echo -e "  ${GREEN}✅ VendorMaster.Api stopped.${RESET}"

restart-vendor: stop-vendor run-vendor

logs-vendor:
	@tail -n 50 -f $(LOG_DIR)/vendor-master.log 2>/dev/null || echo -e "${YELLOW}No log file found for VendorMaster.Api.${RESET}"

# Aliases for VendorMaster
run-vendor-master: run-vendor
stop-vendor-master: stop-vendor
restart-vendor-master: restart-vendor
logs-vendor-master: logs-vendor


# --- 3. PositionQuery.Api (Consolidated into DeliveryTracking.Api :5003) ---
run-position:
	@echo -e "  ${YELLOW}ℹ️  PositionQuery.Api telah dilebur ke DeliveryTracking.Api (:5003). Menjalankan DeliveryTracking.Api...${RESET}"
	@$(MAKE) run-delivery

stop-position:
	@echo -e "  ${YELLOW}ℹ️  PositionQuery.Api telah dilebur ke DeliveryTracking.Api (:5003). Menghentikan DeliveryTracking.Api...${RESET}"
	@$(MAKE) stop-delivery

restart-position:
	@echo -e "  ${YELLOW}ℹ️  PositionQuery.Api telah dilebur ke DeliveryTracking.Api (:5003). Merestart DeliveryTracking.Api...${RESET}"
	@$(MAKE) restart-delivery

logs-position:
	@$(MAKE) logs-delivery

# Aliases for PositionQuery
run-position-query: run-position
stop-position-query: stop-position
restart-position-query: restart-position
logs-position-query: logs-position


# --- 4. DeliveryTracking.Api (:5003) ---
run-delivery:
	@mkdir -p $(PID_DIR) $(LOG_DIR)
	@if [ -f "$(PID_DIR)/delivery-tracking.pid" ] && kill -0 $$(cat $(PID_DIR)/delivery-tracking.pid) 2>/dev/null; then \
		echo -e "  ${YELLOW}⚠️  DeliveryTracking.Api is already running (PID: $$(cat $(PID_DIR)/delivery-tracking.pid))${RESET}"; \
	else \
		echo -e "  ${GREEN}▶ Starting DeliveryTracking.Api (:5003)...${RESET}"; \
		setsid nohup dotnet run --no-build --project src/Services/DeliveryTracking.Api --launch-profile http < /dev/null > $(LOG_DIR)/delivery-tracking.log 2>&1 & \
		PID=$$!; \
		echo $$PID > $(PID_DIR)/delivery-tracking.pid; \
		sleep 1.5; \
		if kill -0 $$PID 2>/dev/null; then \
			echo -e "  ${GREEN}✅ DeliveryTracking.Api started (PID: $$PID) at http://localhost:5003/swagger${RESET}"; \
		else \
			echo -e "  ${RED}❌ Failed to start DeliveryTracking.Api! Check log: $(LOG_DIR)/delivery-tracking.log${RESET}"; \
		fi; \
	fi

stop-delivery:
	@echo -e "  ${YELLOW}🛑 Stopping DeliveryTracking.Api...${RESET}"
	@if [ -f "$(PID_DIR)/delivery-tracking.pid" ]; then \
		PID=$$(cat $(PID_DIR)/delivery-tracking.pid); \
		if kill -0 $$PID 2>/dev/null; then \
			kill -TERM $$PID 2>/dev/null || true; \
			for i in {1..5}; do kill -0 $$PID 2>/dev/null || break; sleep 0.2; done; \
			kill -9 $$PID 2>/dev/null || true; \
		fi; \
		rm -f $(PID_DIR)/delivery-tracking.pid; \
	fi
	@pkill -f 'DeliveryTracking\.Api\.dll' 2>/dev/null || true
	@echo -e "  ${GREEN}✅ DeliveryTracking.Api stopped.${RESET}"

restart-delivery: stop-delivery run-delivery

logs-delivery:
	@tail -n 50 -f $(LOG_DIR)/delivery-tracking.log 2>/dev/null || echo -e "${YELLOW}No log file found for DeliveryTracking.Api.${RESET}"

# Aliases for DeliveryTracking
run-delivery-tracking: run-delivery
stop-delivery-tracking: stop-delivery
restart-delivery-tracking: restart-delivery
logs-delivery-tracking: logs-delivery


# --- 5. GpsCollector (:5004) ---
run-collector:
	@mkdir -p $(PID_DIR) $(LOG_DIR)
	@if [ -f "$(PID_DIR)/gps-collector.pid" ] && kill -0 $$(cat $(PID_DIR)/gps-collector.pid) 2>/dev/null; then \
		echo -e "  ${YELLOW}⚠️  GpsCollector is already running (PID: $$(cat $(PID_DIR)/gps-collector.pid))${RESET}"; \
	else \
		echo -e "  ${GREEN}▶ Starting GpsCollector Background Worker (:5004)...${RESET}"; \
		setsid nohup dotnet run --no-build --project src/Services/GpsCollector --launch-profile http < /dev/null > $(LOG_DIR)/gps-collector.log 2>&1 & \
		PID=$$!; \
		echo $$PID > $(PID_DIR)/gps-collector.pid; \
		sleep 1.5; \
		if kill -0 $$PID 2>/dev/null; then \
			echo -e "  ${GREEN}✅ GpsCollector started (PID: $$PID)${RESET}"; \
		else \
			echo -e "  ${RED}❌ Failed to start GpsCollector! Check log: $(LOG_DIR)/gps-collector.log${RESET}"; \
		fi; \
	fi

stop-collector:
	@echo -e "  ${YELLOW}🛑 Stopping GpsCollector...${RESET}"
	@if [ -f "$(PID_DIR)/gps-collector.pid" ]; then \
		PID=$$(cat $(PID_DIR)/gps-collector.pid); \
		if kill -0 $$PID 2>/dev/null; then \
			kill -TERM $$PID 2>/dev/null || true; \
			for i in {1..5}; do kill -0 $$PID 2>/dev/null || break; sleep 0.2; done; \
			kill -9 $$PID 2>/dev/null || true; \
		fi; \
		rm -f $(PID_DIR)/gps-collector.pid; \
	fi
	@pkill -f 'GpsCollector\.dll' 2>/dev/null || true
	@echo -e "  ${GREEN}✅ GpsCollector stopped.${RESET}"

restart-collector: stop-collector run-collector

logs-collector:
	@tail -n 50 -f $(LOG_DIR)/gps-collector.log 2>/dev/null || echo -e "${YELLOW}No log file found for GpsCollector.${RESET}"

# Aliases for GpsCollector
run-gps-collector: run-collector
stop-gps-collector: stop-collector
restart-gps-collector: restart-collector
logs-gps-collector: logs-collector


# --- Generic Dispatchers (e.g. make run s=vendor) ---
s ?= $(svc)
run:
	@case "$(s)" in \
		gateway|api-gateway) $(MAKE) run-gateway ;; \
		vendor|vendor-master) $(MAKE) run-vendor ;; \
		position|position-query) $(MAKE) run-position ;; \
		delivery|delivery-tracking) $(MAKE) run-delivery ;; \
		collector|gps-collector) $(MAKE) run-collector ;; \
		*) echo -e "${RED}Unknown service: '$(s)'. Usage: make run s=[gateway|vendor|position|delivery|collector]${RESET}" ;; \
	esac

stop:
	@case "$(s)" in \
		gateway|api-gateway) $(MAKE) stop-gateway ;; \
		vendor|vendor-master) $(MAKE) stop-vendor ;; \
		position|position-query) $(MAKE) stop-position ;; \
		delivery|delivery-tracking) $(MAKE) stop-delivery ;; \
		collector|gps-collector) $(MAKE) stop-collector ;; \
		*) echo -e "${RED}Unknown service: '$(s)'. Usage: make stop s=[gateway|vendor|position|delivery|collector]${RESET}" ;; \
	esac

restart:
	@case "$(s)" in \
		gateway|api-gateway) $(MAKE) restart-gateway ;; \
		vendor|vendor-master) $(MAKE) restart-vendor ;; \
		position|position-query) $(MAKE) restart-position ;; \
		delivery|delivery-tracking) $(MAKE) restart-delivery ;; \
		collector|gps-collector) $(MAKE) restart-collector ;; \
		*) echo -e "${RED}Unknown service: '$(s)'. Usage: make restart s=[gateway|vendor|position|delivery|collector]${RESET}" ;; \
	esac

logs:
	@case "$(s)" in \
		gateway|api-gateway) $(MAKE) logs-gateway ;; \
		vendor|vendor-master) $(MAKE) logs-vendor ;; \
		position|position-query) $(MAKE) logs-position ;; \
		delivery|delivery-tracking) $(MAKE) logs-delivery ;; \
		collector|gps-collector) $(MAKE) logs-collector ;; \
		*) echo -e "${RED}Unknown service: '$(s)'. Usage: make logs s=[gateway|vendor|position|delivery|collector]${RESET}" ;; \
	esac

# ------------------------------------------------------------------------------
# 💻 Local PC Microservices — Orchestration All Targets
# ------------------------------------------------------------------------------
run-local-all: infra-up build
	@echo -e "${BOLD}${CYAN}🚀 Launching Lean Microservices locally on Host PC...${RESET}"
	@$(MAKE) run-vendor
	@$(MAKE) run-delivery
	@$(MAKE) run-collector
	@$(MAKE) run-gateway
	@echo -e ""
	@echo -e "${BOLD}${GREEN}========================================================================${RESET}"
	@echo -e "${BOLD}${GREEN}🎉 ALL LEAN MICROSERVICES ARE RUNNING LOCALLY ON HOST PC!${RESET}"
	@echo -e "${BOLD}${GREEN}========================================================================${RESET}"
	@echo -e "  • ${BOLD}YARP Gateway${RESET}        : ${CYAN}http://localhost:5000${RESET}"
	@echo -e "  • ${BOLD}VendorMaster.Api${RESET}    : ${CYAN}http://localhost:5001/swagger${RESET} (Metrics: /metrics)"
	@echo -e "  • ${BOLD}DeliveryTracking.Api${RESET}: ${CYAN}http://localhost:5003/swagger${RESET} (Live Positions & Route Tracking)"
	@echo -e "  • ${BOLD}GpsCollector Worker${RESET} : Background Ingestion Daemon active (:5004)"
	@echo -e ""
	@echo -e "💡 View live logs: ${YELLOW}make logs-local${RESET}"
	@echo -e "🛑 Stop all      : ${YELLOW}make stop-local-all${RESET}"

stop-local-all:
	@echo -e "${BOLD}${YELLOW}🛑 Stopping all local .NET microservices...${RESET}"
	@$(MAKE) stop-gateway
	@$(MAKE) stop-collector
	@$(MAKE) stop-delivery
	@$(MAKE) stop-vendor
	@echo -e "${BOLD}${GREEN}✅ All local microservices stopped.${RESET}"

restart-local-all: stop-local-all run-local-all

logs-local:
	@echo -e "${BOLD}${CYAN}📋 Streaming local logs from $(LOG_DIR)... (Press Ctrl+C to exit)${RESET}"
	@tail -n 25 -f $(LOG_DIR)/*.log 2>/dev/null || echo -e "${YELLOW}No logs found yet. Run 'make run-local-all' first.${RESET}"

# ------------------------------------------------------------------------------
# 📊 Status & Health Check
# ------------------------------------------------------------------------------
status:
	@echo -e "${BOLD}${CYAN}========================================================================${RESET}"
	@echo -e "${BOLD}${CYAN} 📦 DOCKER CONTAINERS STATUS${RESET}"
	@echo -e "${BOLD}${CYAN}========================================================================${RESET}"
	@docker compose $(COMPOSE_FILES) ps
	@echo -e ""
	@echo -e "${BOLD}${CYAN}========================================================================${RESET}"
	@echo -e "${BOLD}${CYAN} 💻 LOCAL PC PROCESSES STATUS${RESET}"
	@echo -e "${BOLD}${CYAN}========================================================================${RESET}"
	@if [ -d "$(PID_DIR)" ] && ls $(PID_DIR)/*.pid >/dev/null 2>&1; then \
		for pidfile in $(PID_DIR)/*.pid; do \
			service=$$(basename $$pidfile .pid); \
			pid=$$(cat $$pidfile); \
			if kill -0 $$pid 2>/dev/null; then \
				echo -e "  • $$(printf '%-22s' $$service) : ${GREEN}RUNNING${RESET} (PID: $$pid)"; \
			else \
				echo -e "  • $$(printf '%-22s' $$service) : ${RED}STOPPED${RESET} (stale PID: $$pid)"; \
			fi; \
		done; \
	else \
		echo -e "  ${YELLOW}No local .NET background services tracked in $(PID_DIR).${RESET}"; \
	fi

health:
	@echo -e "${BOLD}${CYAN}🔍 Probing Microservices Health Endpoints (/hc)...${RESET}"
	@declare -A services=( [5000]="YARP ApiGateway" [5001]="VendorMaster.Api" [5003]="DeliveryTracking.Api" [5004]="GpsCollector Worker" ); \
	for port in 5000 5001 5003 5004; do \
		name=$${services[$$port]}; \
		code=$$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 2 http://localhost:$$port/hc 2>/dev/null); \
		if [ "$$code" = "200" ]; then \
			echo -e "  • Port $$port ($$name) : ${GREEN}HEALTHY (200 OK)${RESET}"; \
		else \
			echo -e "  • Port $$port ($$name) : ${RED}UNREACHABLE / DOWN (code: $$code)${RESET}"; \
		fi; \
	done

# ------------------------------------------------------------------------------
# 🛠️ Build, Test & Maintenance
# ------------------------------------------------------------------------------
build:
	@echo -e "${BOLD}${CYAN}🔨 Building Entire Solution...${RESET}"
	@dotnet build $(SOLUTION)

build-aot:
	@echo -e "${BOLD}${CYAN}⚡ Publishing ApiGateway with Native AOT Compilation...${RESET}"
	@dotnet publish src/Services/ApiGateway/ApiGateway.csproj -c Release -r linux-x64
	@echo -e "${BOLD}${GREEN}✅ Native AOT binary generated at src/Services/ApiGateway/bin/Release/net10.0/linux-x64/publish/ApiGateway${RESET}"

docker-build-aot:
	@echo -e "${BOLD}${CYAN}🐳 Building Ultra-Slim Native AOT Docker Container (Chiseled)...${RESET}"
	@docker build -f src/Services/ApiGateway/Dockerfile -t edcl-api-gateway:aot .
	@echo -e "${BOLD}${GREEN}✅ Docker image edcl-api-gateway:aot built successfully!${RESET}"

test:
	@echo -e "${BOLD}${CYAN}🧪 Running Unit & Integration Tests...${RESET}"
	@dotnet test $(SOLUTION)

test-delivery-sim:
	@bash scripts/test_delivery_happy_path.sh $(do)

test-happy-path: test-delivery-sim

clean:
	@echo -e "${BOLD}${YELLOW}🧹 Cleaning Solution and Artifacts...${RESET}"
	@dotnet clean $(SOLUTION)
	@rm -rf $(PID_DIR) $(LOG_DIR)
	@echo -e "${BOLD}${GREEN}✅ Clean completed.${RESET}"

# ------------------------------------------------------------------------------
# 🔍 SonarQube Static Analysis & Quality Gate Targets
# ------------------------------------------------------------------------------
sonar-up:
	@echo -e "${BOLD}${CYAN}🔍 Checking Linux kernel settings for Elasticsearch/SonarQube...${RESET}"
	@current_max=$$(sysctl -n vm.max_map_count 2>/dev/null || echo 0); \
	if [ "$$current_max" -lt 262144 ]; then \
		echo -e "${YELLOW}⚠️  vm.max_map_count is $$current_max (minimum 262144 required).${RESET}"; \
		echo -e "${YELLOW}👉 Run: sudo sysctl -w vm.max_map_count=262144${RESET}"; \
	fi
	@echo -e "${BOLD}${CYAN}🚀 Starting SonarQube Community Edition & Dedicated PostgreSQL...${RESET}"
	@docker compose $(SONAR_COMPOSE) up -d
	@echo -e "${BOLD}${GREEN}⏳ Waiting for SonarQube Web UI to initialize (http://localhost:9000)...${RESET}"
	@for i in {1..40}; do \
		status=$$(curl -s http://localhost:9000/api/system/status 2>/dev/null | grep -o '"status":"[^"]*"' | cut -d'"' -f4); \
		if [ "$$status" = "UP" ]; then \
			echo -e "${BOLD}${GREEN}✅ SonarQube is UP and Ready! Access at: http://localhost:9000 (Default login: admin/admin)${RESET}"; \
			exit 0; \
		fi; \
		sleep 3; \
	done; \
	echo -e "${YELLOW}ℹ️  SonarQube container is starting up in background. Run 'make sonar-status' or 'make sonar-logs' to monitor.${RESET}"

sonar-down:
	@echo -e "${BOLD}${YELLOW}🛑 Stopping SonarQube containers...${RESET}"
	@docker compose $(SONAR_COMPOSE) down
	@echo -e "${BOLD}${GREEN}✅ SonarQube stopped.${RESET}"

sonar-restart: sonar-down sonar-up

sonar-status:
	@echo -e "${BOLD}${CYAN}📊 SonarQube Containers Status:${RESET}"
	@docker compose $(SONAR_COMPOSE) ps
	@echo -e "${BOLD}${CYAN}🔍 System API Status:${RESET}"
	@curl -s http://localhost:9000/api/system/status 2>/dev/null || echo -e "${RED}❌ SonarQube Web API unreachable on http://localhost:9000${RESET}"

sonar-logs:
	@docker compose $(SONAR_COMPOSE) logs -f sonarqube

sonar-clean:
	@echo -e "${BOLD}${RED}🧹 Stopping SonarQube & Deleting Data Volumes...${RESET}"
	@docker compose $(SONAR_COMPOSE) down -v
	@echo -e "${BOLD}${GREEN}✅ SonarQube cleaned.${RESET}"

sonar-scan:
	@echo -e "${BOLD}${CYAN}🔍 Verifying SonarQube scanner environment...${RESET}"
	@which java > /dev/null 2>&1 || (echo -e "${RED}❌ Java is required for SonarScanner. Please install Java 17+ or ensure it is on PATH.${RESET}" && exit 1)
	@dotnet tool list -g | grep -q "dotnet-sonarscanner" || (echo -e "${YELLOW}📦 Installing dotnet-sonarscanner global tool...${RESET}" && dotnet tool install --global dotnet-sonarscanner)
	@curl -s http://localhost:9000/api/system/status > /dev/null 2>&1 || (echo -e "${RED}❌ SonarQube Server is not running at $(SONAR_HOST_URL). Run 'make sonar-up' first!${RESET}" && exit 1)
	@TOKEN_VAL="$(SONAR_TOKEN)"; \
	if [ -z "$$TOKEN_VAL" ]; then \
		TOKEN_VAL="$$SONAR_TOKEN"; \
	fi; \
	if [ -z "$$TOKEN_VAL" ]; then \
		echo -e "${RED}❌ Missing SonarQube Token!${RESET}"; \
		echo -e "${YELLOW}Usage: make sonar-scan token=<YOUR_SONAR_TOKEN>${RESET}"; \
		echo -e "${YELLOW}Or:    export SONAR_TOKEN=<YOUR_SONAR_TOKEN> && make sonar-scan${RESET}"; \
		exit 1; \
	fi; \
	echo -e "${BOLD}${CYAN}🚀 [1/4] Initializing SonarQube Scanner Begin...${RESET}"; \
	dotnet sonarscanner begin \
		/k:"$(SONAR_PROJECT_KEY)" \
		/n:"$(SONAR_PROJECT_NAME)" \
		/d:sonar.host.url="$(SONAR_HOST_URL)" \
		/d:sonar.token="$$TOKEN_VAL" \
		/d:sonar.cs.opencover.reportsPaths="tests/**/TestResults/**/coverage.opencover.xml" \
		/d:sonar.coverage.exclusions="**/Program.cs,**/Dockerfile,**/Migrations/**,**/*.sql,src/Shared/Shared.Contracts/**" \
		/d:sonar.cpd.exclusions="src/Shared/Shared.Contracts/**" && \
	echo -e "${BOLD}${CYAN}🔨 [2/4] Building Solution (Release)...${RESET}" && \
	dotnet build $(SOLUTION) -c Release && \
	echo -e "${BOLD}${CYAN}🧪 [3/4] Running Tests & Collecting OpenCover Coverage...${RESET}" && \
	dotnet test $(SOLUTION) -c Release --no-build --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover && \
	echo -e "${BOLD}${CYAN}📤 [4/4] Finalizing SonarQube Scan & Uploading Report...${RESET}" && \
	dotnet sonarscanner end /d:sonar.token="$$TOKEN_VAL" && \
	echo -e "${BOLD}${GREEN}✅ SonarQube Analysis Completed! View results at: $(SONAR_HOST_URL)/dashboard?id=$(SONAR_PROJECT_KEY)${RESET}"
