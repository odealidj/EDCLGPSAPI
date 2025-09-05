# ==============================================================================
# 🛰️ EDCL GPS API — Microservices Platform Automation Makefile
# ==============================================================================

SHELL := /bin/bash
COMPOSE_FILE := src/docker-compose.yml
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
        build test clean health \
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
        docker-restart docker-logs docker-stop docker-run

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
	@echo -e "${BOLD}${YELLOW}💻 LOCAL PC MICROSERVICES (All 5 .NET Services):${RESET}"
	@echo -e "  ${GREEN}make run-local-all${RESET}         Start infra in Docker + all 5 .NET services locally"
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
	@echo -e "  ${GREEN}make run-position${RESET}          Start PositionQuery.Api (:5002)"
	@echo -e "  ${GREEN}make stop-position${RESET}         Stop PositionQuery.Api"
	@echo -e "  ${GREEN}make restart-position${RESET}      Restart PositionQuery.Api"
	@echo -e "  ${GREEN}make logs-position${RESET}         Tail logs of PositionQuery.Api"
	@echo -e ""
	@echo -e "  ${GREEN}make run-delivery${RESET}          Start DeliveryTracking.Api (:5003)"
	@echo -e "  ${GREEN}make stop-delivery${RESET}         Stop DeliveryTracking.Api"
	@echo -e "  ${GREEN}make restart-delivery${RESET}      Restart DeliveryTracking.Api"
	@echo -e "  ${GREEN}make logs-delivery${RESET}         Tail logs of DeliveryTracking.Api"
	@echo -e ""
	@echo -e "  ${GREEN}make run-collector${RESET}         Start GpsCollector Background Worker (:5004)"
	@echo -e "  ${GREEN}make stop-collector${RESET}        Stop GpsCollector Background Worker"
	@echo -e "  ${GREEN}make restart-collector${RESET}     Restart GpsCollector Background Worker"
	@echo -e "  ${GREEN}make logs-collector${RESET}        Tail logs of GpsCollector"
	@echo -e ""
	@echo -e "  ${CYAN}💡 Parameterized syntax: make run s=<svc> | make stop s=<svc> | make restart s=<svc>${RESET}"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}📦 DOCKER ALL-IN-ONE (Run Entire Platform inside Docker Compose):${RESET}"
	@echo -e "  ${GREEN}make run-all${RESET}               Build & start everything (5 services + infra) in Docker"
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
	@echo -e "  ${GREEN}make test${RESET}                  Run tests (dotnet test)"
	@echo -e "  ${GREEN}make clean${RESET}                 Clean build artifacts (bin/obj) & local log/pid files"
	@echo -e ""

# ------------------------------------------------------------------------------
# 🧱 Infrastructure Only Targets
# ------------------------------------------------------------------------------
infra-up:
	@echo -e "${BOLD}${CYAN}🚀 Starting Infrastructure Stack (PostgreSQL, MongoDB, Redis, RabbitMQ, Prometheus, Grafana, Seq)...${RESET}"
	@docker compose -f $(COMPOSE_FILE) up -d $(INFRA_SERVICES)
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
	@docker compose -f $(COMPOSE_FILE) stop $(INFRA_SERVICES)
	@echo -e "${BOLD}${GREEN}✅ Infrastructure containers stopped.${RESET}"

infra-restart: infra-down infra-up

# ------------------------------------------------------------------------------
# 📦 Docker Targets
# ------------------------------------------------------------------------------
run-all:
	@echo -e "${BOLD}${CYAN}🚀 Building and Starting Entire Platform in Docker Compose...${RESET}"
	@docker compose -f $(COMPOSE_FILE) up -d --build
	@echo -e "${BOLD}${GREEN}✅ All microservices & infrastructure are running in Docker!${RESET}"
	@echo -e "  • YARP API Gateway  : http://localhost:5000 (or :8080)"
	@echo -e "  • VendorMaster API  : http://localhost:5001/swagger"
	@echo -e "  • PositionQuery API : http://localhost:5002/swagger"
	@echo -e "  • DeliveryTrack API : http://localhost:5003/swagger"
	@echo -e "  • Prometheus        : http://localhost:9090"
	@echo -e "  • Grafana           : http://localhost:3000"

stop-all:
	@echo -e "${BOLD}${YELLOW}🛑 Tearing down all Docker Compose containers...${RESET}"
	@docker compose -f $(COMPOSE_FILE) down
	@echo -e "${BOLD}${GREEN}✅ All Docker containers stopped and removed.${RESET}"

restart-all: stop-all run-all

logs-all:
	@docker compose -f $(COMPOSE_FILE) logs -f

docker-restart:
	@if [ -z "$(s)" ]; then \
		echo -e "${RED}Error: Service name required. Example: make docker-restart s=vendor-master-svc${RESET}"; \
	else \
		docker compose -f $(COMPOSE_FILE) restart $(s); \
	fi

docker-logs:
	@if [ -z "$(s)" ]; then \
		echo -e "${RED}Error: Service name required. Example: make docker-logs s=vendor-master-svc${RESET}"; \
	else \
		docker compose -f $(COMPOSE_FILE) logs -f $(s); \
	fi

docker-stop:
	@if [ -z "$(s)" ]; then \
		echo -e "${RED}Error: Service name required. Example: make docker-stop s=vendor-master-svc${RESET}"; \
	else \
		docker compose -f $(COMPOSE_FILE) stop $(s); \
	fi

docker-run:
	@if [ -z "$(s)" ]; then \
		echo -e "${RED}Error: Service name required. Example: make docker-run s=vendor-master-svc${RESET}"; \
	else \
		docker compose -f $(COMPOSE_FILE) up -d $(s); \
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
		dotnet run --project src/Services/ApiGateway --launch-profile http > $(LOG_DIR)/api-gateway.log 2>&1 & \
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
		dotnet run --project src/Services/VendorMaster.Api --launch-profile http > $(LOG_DIR)/vendor-master.log 2>&1 & \
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


# --- 3. PositionQuery.Api (:5002) ---
run-position:
	@mkdir -p $(PID_DIR) $(LOG_DIR)
	@if [ -f "$(PID_DIR)/position-query.pid" ] && kill -0 $$(cat $(PID_DIR)/position-query.pid) 2>/dev/null; then \
		echo -e "  ${YELLOW}⚠️  PositionQuery.Api is already running (PID: $$(cat $(PID_DIR)/position-query.pid))${RESET}"; \
	else \
		echo -e "  ${GREEN}▶ Starting PositionQuery.Api (:5002)...${RESET}"; \
		dotnet run --project src/Services/PositionQuery.Api --launch-profile http > $(LOG_DIR)/position-query.log 2>&1 & \
		PID=$$!; \
		echo $$PID > $(PID_DIR)/position-query.pid; \
		sleep 1.5; \
		if kill -0 $$PID 2>/dev/null; then \
			echo -e "  ${GREEN}✅ PositionQuery.Api started (PID: $$PID) at http://localhost:5002/swagger${RESET}"; \
		else \
			echo -e "  ${RED}❌ Failed to start PositionQuery.Api! Check log: $(LOG_DIR)/position-query.log${RESET}"; \
		fi; \
	fi

stop-position:
	@echo -e "  ${YELLOW}🛑 Stopping PositionQuery.Api...${RESET}"
	@if [ -f "$(PID_DIR)/position-query.pid" ]; then \
		PID=$$(cat $(PID_DIR)/position-query.pid); \
		if kill -0 $$PID 2>/dev/null; then \
			kill -TERM $$PID 2>/dev/null || true; \
			for i in {1..5}; do kill -0 $$PID 2>/dev/null || break; sleep 0.2; done; \
			kill -9 $$PID 2>/dev/null || true; \
		fi; \
		rm -f $(PID_DIR)/position-query.pid; \
	fi
	@pkill -f 'PositionQuery\.Api\.dll' 2>/dev/null || true
	@echo -e "  ${GREEN}✅ PositionQuery.Api stopped.${RESET}"

restart-position: stop-position run-position

logs-position:
	@tail -n 50 -f $(LOG_DIR)/position-query.log 2>/dev/null || echo -e "${YELLOW}No log file found for PositionQuery.Api.${RESET}"

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
		dotnet run --project src/Services/DeliveryTracking.Api --launch-profile http > $(LOG_DIR)/delivery-tracking.log 2>&1 & \
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
		dotnet run --project src/Services/GpsCollector --launch-profile http > $(LOG_DIR)/gps-collector.log 2>&1 & \
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
run-local-all: infra-up
	@echo -e "${BOLD}${CYAN}🚀 Launching all 5 Microservices locally on Host PC...${RESET}"
	@$(MAKE) run-position
	@$(MAKE) run-vendor
	@$(MAKE) run-delivery
	@$(MAKE) run-collector
	@$(MAKE) run-gateway
	@echo -e ""
	@echo -e "${BOLD}${GREEN}========================================================================${RESET}"
	@echo -e "${BOLD}${GREEN}🎉 ALL 5 MICROSERVICES ARE RUNNING LOCALLY ON HOST PC!${RESET}"
	@echo -e "${BOLD}${GREEN}========================================================================${RESET}"
	@echo -e "  • ${BOLD}YARP Gateway${RESET}        : ${CYAN}http://localhost:5000${RESET}"
	@echo -e "  • ${BOLD}VendorMaster.Api${RESET}    : ${CYAN}http://localhost:5001/swagger${RESET} (Metrics: /metrics)"
	@echo -e "  • ${BOLD}PositionQuery.Api${RESET}   : ${CYAN}http://localhost:5002/swagger${RESET} (Metrics: /metrics)"
	@echo -e "  • ${BOLD}DeliveryTracking.Api${RESET}: ${CYAN}http://localhost:5003/swagger${RESET} (Metrics: /metrics)"
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
	@$(MAKE) stop-position
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
	@docker compose -f $(COMPOSE_FILE) ps
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
	@for port in 5000 5001 5002 5003; do \
		echo -n "  • Port $$port : "; \
		curl -s -o /dev/null -w "%{http_code}" --connect-timeout 2 http://localhost:$$port/hc 2>/dev/null && echo " OK" || echo " UNREACHABLE"; \
	done

# ------------------------------------------------------------------------------
# 🛠️ Build, Test & Maintenance
# ------------------------------------------------------------------------------
build:
	@echo -e "${BOLD}${CYAN}🔨 Building Entire Solution...${RESET}"
	@dotnet build $(SOLUTION)

test:
	@echo -e "${BOLD}${CYAN}🧪 Running Unit & Integration Tests...${RESET}"
	@dotnet test $(SOLUTION)

clean:
	@echo -e "${BOLD}${YELLOW}🧹 Cleaning Solution and Artifacts...${RESET}"
	@dotnet clean $(SOLUTION)
	@rm -rf $(PID_DIR) $(LOG_DIR)
	@echo -e "${BOLD}${GREEN}✅ Clean completed.${RESET}"
