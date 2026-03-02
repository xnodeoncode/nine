// Global usings for Application layer
// ApplicationUser removed - now product-specific
// Application layer uses ApplicationDbContext for business data only
global using ApplicationDbContext = Nine.Infrastructure.Data.ApplicationDbContext;
global using SendGridEmailService = Nine.Infrastructure.Core.Services.SendGridEmailService;
global using TwilioSMSService = Nine.Infrastructure.Core.Services.TwilioSMSService;
