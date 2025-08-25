output "rds_endpoint" {
  value       = aws_db_instance.edcl_postgres.address
  description = "Amazon RDS PostgreSQL Endpoint"
}

output "redis_primary_endpoint" {
  value       = aws_elasticache_replication_group.edcl_redis.primary_endpoint_address
  description = "Amazon ElastiCache Redis Primary Endpoint"
}
