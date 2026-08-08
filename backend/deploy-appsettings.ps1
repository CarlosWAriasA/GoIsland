<#
Sube los secretos de appsettings.Development.json como App Settings de Azure App Service.
Requiere: az cli instalado, sesion iniciada (az login) y el Web App ya creado.

Uso:
  ./deploy-appsettings.ps1 -AppName goisland-api-carlos -ResourceGroup goisland-rg -FrontendUrl https://tu-frontend.com
#>
param(
    [Parameter(Mandatory = $true)][string]$AppName,
    [Parameter(Mandatory = $true)][string]$ResourceGroup,
    [Parameter(Mandatory = $true)][string]$FrontendUrl
)

$ErrorActionPreference = "Stop"

$settingsPath = Join-Path $PSScriptRoot "GoIsland.Api\appsettings.Development.json"
if (-not (Test-Path $settingsPath)) {
    throw "No se encontro $settingsPath"
}
$json = Get-Content $settingsPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($json.Jwt.Key)) {
    $bytes = New-Object byte[] 64
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $jwtKey = [Convert]::ToBase64String($bytes)
    Write-Host "Jwt:Key estaba vacio en appsettings.Development.json -> se genero una clave nueva para produccion."
}
else {
    $jwtKey = $json.Jwt.Key
}

$appHost = "$AppName.azurewebsites.net"

$settings = [ordered]@{
    "ConnectionStrings__DefaultConnection" = $json.ConnectionStrings.DefaultConnection
    "Jwt__Issuer"                          = $json.Jwt.Issuer
    "Jwt__Audience"                        = $json.Jwt.Audience
    "Jwt__Key"                             = $jwtKey
    "GoogleAuth__ClientId"                 = $json.GoogleAuth.ClientId
    "GoogleMaps__ApiKey"                   = $json.GoogleMaps.ApiKey
    "Email__Provider"                      = $json.Email.Provider
    "Email__FromEmail"                     = $json.Email.FromEmail
    "Email__FromName"                      = $json.Email.FromName
    "Email__ResetPasswordUrl"              = "$FrontendUrl/reset-password"
    "Smtp__Host"                           = $json.Smtp.Host
    "Smtp__Port"                           = "$($json.Smtp.Port)"
    "Smtp__EnableSsl"                      = "$($json.Smtp.EnableSsl)".ToLower()
    "Smtp__Username"                       = $json.Smtp.Username
    "Smtp__Password"                       = $json.Smtp.Password
    "WebPush__Subject"                     = $json.WebPush.Subject
    "WebPush__PublicKey"                   = $json.WebPush.PublicKey
    "WebPush__PrivateKey"                  = $json.WebPush.PrivateKey
    "Payments__Provider"                   = $json.Payments.Provider
    "Payments__Mode"                       = $json.Payments.Mode
    "Payments__Currency"                   = $json.Payments.Currency
    "Payments__ServiceFeePercent"          = "$($json.Payments.ServiceFeePercent)"
    "Payments__CommissionPercent"          = "$($json.Payments.CommissionPercent)"
    "Stripe__SecretKey"                    = $json.Stripe.SecretKey
    "Stripe__WebhookSecret"                = $json.Stripe.WebhookSecret
    "Cloudinary__CloudName"                = $json.Cloudinary.CloudName
    "Cloudinary__ApiKey"                   = $json.Cloudinary.ApiKey
    "Cloudinary__ApiSecret"                = $json.Cloudinary.ApiSecret
    "Cors__FrontendUrl"                    = $FrontendUrl
    "ForwardedHeaders__TrustManagedProxy"  = "true"
    "AllowedHosts"                         = $appHost
    "ASPNETCORE_ENVIRONMENT"               = "Production"
}

$settingsArray = $settings.GetEnumerator() | ForEach-Object {
    [PSCustomObject]@{ name = $_.Key; value = $_.Value; slotSetting = $false }
}

$tmpFile = New-TemporaryFile
try {
    $settingsArray | ConvertTo-Json | Set-Content -Path $tmpFile -Encoding utf8

    Write-Host "Aplicando $($settingsArray.Count) app settings a $AppName..."
    az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings "@$tmpFile" | Out-Null
    Write-Host "Listo."
}
finally {
    Remove-Item $tmpFile -ErrorAction SilentlyContinue
}
