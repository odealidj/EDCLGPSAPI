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
        build test clean health

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
	@echo -e "  ${GREEN}make infra-up${RESET}          Start all infrastructure containers in Docker"
	@echo -e "  ${GREEN}make infra-down${RESET}        Stop infrastructure containers"
	@echo -e "  ${GREEN}make infra-restart${RESET}     Restart infrastructure containers"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}💻 LOCAL PC MICROSERVICES (Run .NET Services on Host PC):${RESET}"
	@echo -e "  ${GREEN}make run-local-all${RESET}     Start infra in Docker + all 5 .NET services locally"
	@echo -e "  ${GREEN}make stop-local-all${RESET}    Stop all locally running .NET microservices cleanly"
	@echo -e "  ${GREEN}make restart-local-all${RESET} Restart all local services"
	@echo -e "  ${GREEN}make logs-local${RESET}        Tail live console logs of all local services"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}📦 DOCKER ALL-IN-ONE (Run Entire Platform inside Docker Compose):${RESET}"
	@echo -e "  ${GREEN}make run-all${RESET}           Build & start everything (5 services + infra) in Docker"
	@echo -e "  ${GREEN}make stop-all${RESET}          Stop & teardown all Docker containers"
	@echo -e "  ${GREEN}make restart-all${RESET}       Restart all Docker containers"
	@echo -e "  ${GREEN}make logs-all${RESET}          Tail live logs of all Docker containers"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}📊 STATUS & MONITORING:${RESET}"
	@echo -e "  ${GREEN}make status${RESET}            Display running containers, local processes & ports"
	@echo -e "  ${GREEN}make health${RESET}            Probe /hc and /metrics endpoints across services"
	@echo -e ""
	@echo -e "${BOLD}${YELLOW}🛠️  BUILD & MAINTENANCE:${RESET}"
	@echo -e "  ${GREEN}make build${RESET}             Compile the entire solution (dotnet build)"
	@echo -e "  ${GREEN}make test${RESET}              Run tests (dotnet test)"
	@echo -e "  ${GREEN}make clean${RESET}             Clean build artifacts (bin/obj) & local log/pid files"
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
# 📦 Docker All-In-One Targets
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

# ------------------------------------------------------------------------------
# 💻 Local PC Microservices (Host .NET execution)
# ------------------------------------------------------------------------------
run-local-all: infra-up
	@echo -e "${BOLD}${CYAN}🚀 Launching 5 Microservices locally on Host PC...${RESET}"
	@mkdir -p $(PID_DIR) $(LOG_DIR)
	@$(MAKE) stop-local-all > /dev/null 2>&1 || true
	@echo -e "  ${GREEN}▶ Starting PositionQuery.Api (:5002)...${RESET}"
	@dotnet run --project src/Services/PositionQuery.Api --launch-profile http > $(LOG_DIR)/position-query.log 2>&1 & echo $$! > $(PID_DIR)/position-query.pid
	@echo -e "  ${GREEN}▶ Starting VendorMaster.Api (:5001)...${RESET}"
	@dotnet run --project src/Services/VendorMaster.Api --launch-profile http > $(LOG_DIR)/vendor-master.log 2>&1 & echo $$! > $(PID_DIR)/vendor-master.pid
	@echo -e "  ${GREEN}▶ Starting DeliveryTracking.Api (:5003)...${RESET}"
	@dotnet run --project src/Services/DeliveryTracking.Api --launch-profile http > $(LOG_DIR)/delivery-tracking.log 2>&1 & echo $$! > $(PID_DIR)/delivery-tracking.pid
	@echo -e "  ${GREEN}▶ Starting GpsCollector Background Worker...${RESET}"
	@dotnet run --project src/Services/GpsCollector --launch-profile http > $(LOG_DIR)/gps-collector.log 2>&1 & echo $$! > $(PID_DIR)/gps-collector.pid
	@echo -e "  ${GREEN}▶ Starting ApiGateway (:5000)...${RESET}"
	@dotnet run --project src/Services/ApiGateway --launch-profile http > $(LOG_DIR)/api-gateway.log 2>&1 & echo $$! > $(PID_DIR)/api-gateway.pid
	@sleep 2
	@echo -e ""
	@echo -e "${BOLD}${GREEN}========================================================================${RESET}"
	@echo -e "${BOLD}${GREEN}🎉 ALL 5 MICROSERVICES ARE RUNNING LOCALLY ON HOST PC!${RESET}"
	@echo -e "${BOLD}${GREEN}========================================================================${RESET}"
	@echo -e "  • ${BOLD}YARP Gateway${RESET}        : ${CYAN}http://localhost:5000${RESET}"
	@echo -e "  • ${BOLD}VendorMaster.Api${RESET}    : ${CYAN}http://localhost:5001/swagger${RESET} (Metrics: /metrics)"
	@echo -e "  • ${BOLD}PositionQuery.Api${RESET}   : ${CYAN}http://localhost:5002/swagger${RESET} (Metrics: /metrics)"
	@echo -e "  • ${BOLD}DeliveryTracking.Api${RESET}: ${CYAN}http://localhost:5003/swagger${RESET} (Metrics: /metrics)"
	@echo -e "  • ${BOLD}GpsCollector Worker${RESET} : Background Ingestion Daemon active"
	@echo -e ""
	@echo -e "💡 View live logs: ${YELLOW}make logs-local${RESET}"
	@echo -e "🛑 Stop services : ${YELLOW}make stop-local-all${RESET}"

stop-local-all:
	@echo -e "${BOLD}${YELLOW}🛑 Stopping all local .NET microservices...${RESET}"
	@if [ -d "$(PID_DIR)" ]; then \
		for pidfile in $(PID_DIR)/*.pid; do \
			if [ -f "$$pidfile" ]; then \
				pid=$$(cat $$pidfile); \
				if kill -0 $$pid 2>/dev/null; then \
					echo -e "  • Killing process $$pid ($$pidfile)..."; \
					kill -TERM $$pid 2>/dev/null || kill -9 $$pid 2>/dev/null || true; \
				fi; \
				rm -f $$pidfile; \
			fi; \
		done; \
	fi
	@pkill -f 'ApiGateway\.dll|VendorMaster\.Api\.dll|PositionQuery\.Api\.dll|DeliveryTracking\.Api\.dll|GpsCollector\.dll' 2>/dev/null || true
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
