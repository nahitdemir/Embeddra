COMPOSE := docker compose -f infra/docker-compose.yml

.PHONY: help deps-up deps-down deps-ps deps-logs deps-restart health run-admin run-search run-worker dev

help:
	@echo "make deps-up        # start infra services"
	@echo "make deps-down      # stop infra services"
	@echo "make deps-ps        # list infra services"
	@echo "make deps-logs      # follow infra logs"
	@echo "make dev            # start infra + all .NET services"
	@echo "make run-admin      # run Admin API only"
	@echo "make run-search     # run Search API only"
	@echo "make run-worker     # run Worker only"
	@echo "make health         # check local health endpoints"

deps-up:
	$(COMPOSE) up -d

deps-down:
	$(COMPOSE) down

deps-ps:
	$(COMPOSE) ps

deps-logs:
	$(COMPOSE) logs -f --tail=100

deps-restart: deps-down deps-up

run-admin:
	dotnet run --project apps/Admin/Embeddra.Admin.WebApi

run-search:
	dotnet run --project apps/Search/Embeddra.Search.WebApi

run-worker:
	dotnet run --project apps/Worker/Embeddra.Worker.Host

dev: deps-up
	@echo "Starting Admin, Search, Worker. Ctrl+C to stop all."
	@trap 'kill 0' INT TERM; \
		dotnet run --project apps/Admin/Embeddra.Admin.WebApi & \
		dotnet run --project apps/Search/Embeddra.Search.WebApi & \
		dotnet run --project apps/Worker/Embeddra.Worker.Host & \
		wait

health:
	@curl -sSf http://localhost:5114/health >/dev/null && echo "Admin OK" || echo "Admin FAIL"
	@curl -sSf http://localhost:5222/health >/dev/null && echo "Search OK" || echo "Search FAIL"
	@curl -sSf http://localhost:5310/health >/dev/null && echo "Worker OK" || echo "Worker FAIL"
