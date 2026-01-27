-- Post-copy configuration for development database
-- This script adapts the copied production database for the dev environment

-- Update store configuration for dev environment
UPDATE [Store] 
SET [Name] = 'rcc-develop', 
    [Url] = 'https://rcc-develop-wa.azurewebsites.net/', 
    [Hosts] = 'rcc-develop-wa.azurewebsites.net';

-- Disable CAPTCHA on login page for easier dev testing
UPDATE [Setting] 
SET [Value] = 'False' 
WHERE [Name] = 'captchasettings.showonloginpage';

-- Add any additional dev-specific configuration here
