variable "aws_region" {
  type        = string
  default     = "ap-southeast-1"
  description = "AWS Region for EDCL deployment"
}

variable "environment" {
  type        = string
  default     = "production"
  description = "Deployment environment name"
}

variable "vpc_id" {
  type        = string
  description = "VPC ID where managed services reside"
}

variable "subnet_ids" {
  type        = list(string)
  description = "Private Subnet IDs for RDS, ElastiCache, and MQ"
}

variable "db_password" {
  type        = string
  sensitive   = true
  description = "Master password for Amazon RDS PostgreSQL"
}
