terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

# -----------------------------------------------------------------------------
# 1. Security Groups
# -----------------------------------------------------------------------------
resource "aws_security_group" "rds_sg" {
  name        = "edcl-rds-sg"
  description = "Allow inbound PostgreSQL from EKS nodes"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = ["10.0.0.0/16"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "edcl-rds-sg"
    Environment = var.environment
  }
}

resource "aws_security_group" "elasticache_sg" {
  name        = "edcl-elasticache-sg"
  description = "Allow inbound Redis from EKS nodes"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 6379
    to_port     = 6379
    protocol    = "tcp"
    cidr_blocks = ["10.0.0.0/16"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "edcl-elasticache-sg"
    Environment = var.environment
  }
}

# -----------------------------------------------------------------------------
# 2. Amazon RDS PostgreSQL 16 (Multi-AZ HA)
# -----------------------------------------------------------------------------
resource "aws_db_subnet_group" "edcl_db_subnets" {
  name       = "edcl-db-subnets"
  subnet_ids = var.subnet_ids

  tags = {
    Name = "EDCL DB Subnet Group"
  }
}

resource "aws_db_instance" "edcl_postgres" {
  identifier           = "edcl-gps-pg"
  engine               = "postgres"
  engine_version       = "16.3"
  instance_class       = "db.t3.medium"
  allocated_storage    = 100
  max_allocated_storage = 500
  storage_type         = "gp3"
  multi_az             = true

  db_name  = "AE031_EDCL_GPS_DB"
  username = "postgres"
  password = var.db_password

  db_subnet_group_name   = aws_db_subnet_group.edcl_db_subnets.name
  vpc_security_group_ids = [aws_security_group.rds_sg.id]

  skip_final_snapshot     = true
  deletion_protection     = false
  backup_retention_period = 7
  auto_minor_version_upgrade = true

  tags = {
    Name        = "edcl-gps-pg"
    Environment = var.environment
  }
}

# -----------------------------------------------------------------------------
# 3. Amazon ElastiCache Redis 7
# -----------------------------------------------------------------------------
resource "aws_elasticache_subnet_group" "edcl_redis_subnets" {
  name       = "edcl-redis-subnets"
  subnet_ids = var.subnet_ids
}

resource "aws_elasticache_replication_group" "edcl_redis" {
  replication_group_id          = "edcl-gps-redis"
  description                   = "Redis cluster for EDCL telemetry cache & leader election"
  node_type                     = "cache.t3.micro"
  port                          = 6379
  parameter_group_name          = "default.redis7"
  automatic_failover_enabled    = true
  num_cache_clusters            = 2
  subnet_group_name             = aws_elasticache_subnet_group.edcl_redis_subnets.name
  security_group_ids            = [aws_security_group.elasticache_sg.id]
  at_rest_encryption_enabled    = true
  transit_encryption_enabled    = false

  tags = {
    Name        = "edcl-gps-redis"
    Environment = var.environment
  }
}
