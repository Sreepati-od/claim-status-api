#!/bin/bash
# Automated Azure deployment script for vanilla setup
set -e

# Variables (edit as needed)
RG="claimstatus-rg"
LOCATION="eastus"
ACR_NAME="claimstatusacr$RANDOM"

# Create resource group
az group create -n $RG -l $LOCATION

# Deploy all resources with Bicep
az deployment group create \
  -g $RG \
  --template-file iac/main.bicep \
  --parameters acrName=$ACR_NAME \
               acaEnvName=aca-env \
               acaAppName=claimstatusapi \
               apimName=apim-service \
               logAnalyticsName=log-analytics \
               openAiKeyVaultName=openai-kv

echo "Deployment complete!"
