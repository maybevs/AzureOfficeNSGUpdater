# AzureOfficeNSGUpdater
This is a Function App that checks the Office IP API and adds NSG rules to open the required ports.

Currently there is no support for FQDN in NSG Rules so everything without a defined IP range will be ignored.
This is still very early stage and not tested. Thus please handle with care and expect it to fail all the time...


This uses the Managed Service Identity available for Azure Functions. To enable the Function App to change NSG Setting you must first create a Managed Identity (Function Menu) and then give a Role Assignment to the Function (Creator or specialized Role with edit rights on rules).
