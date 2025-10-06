// global usings buat application layer (ports & use cases)
global using System.Text.Json.Nodes;
global using FluentValidation;
global using MediatR;
global using Microsoft.Extensions.Logging;
global using Shared.Contracts.CQRS;
global using Shared.Pagination;
global using VendorMaster.Domain.Constants;
global using VendorMaster.Domain.Entities;
global using VendorMaster.Domain.Enums;
global using VendorMaster.Domain.Exceptions;
global using VendorMaster.Application.DTOs;
global using VendorMaster.Application.Ports;
global using AutoMapper;
