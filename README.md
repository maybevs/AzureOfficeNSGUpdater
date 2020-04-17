# AzureOfficeNSGUpdater
This is a Function App that checks the Office IP API and adds NSG rules to open the required ports.

Currently there is no support for FQDN in NSG Rules so everything without a defined IP range will be ignored.
This is still very early stage and not tested. Thus please handle with care and expect it to fail all the time...
