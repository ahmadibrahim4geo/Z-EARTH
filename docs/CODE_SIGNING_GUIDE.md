# Z EARTH Desktop GIS — Code Signing & Distribution Architecture (ZE-062)

**Product:** Z EARTH Desktop GIS v1.0.0  
**Developer:** Ahmad Ibrahim  
**Target:** Microsoft Windows Authenticode Signing  

---

## 1. Overview & Objectives

Code signing establishes developer identity, prevents operating system security warnings (Microsoft Defender SmartScreen), and guarantees that application binaries have not been tampered with or corrupted during distribution.

This document outlines the standard production signing pipeline for Z EARTH Desktop GIS binaries (`Z EARTH.exe`, companion DLLs, and installer packages).

---

## 2. Prerequisites

1. **Code Signing Certificate:**
   - Standard Commercial Authenticode Certificate (or Extended Validation / EV Certificate on hardware USB token or cloud HSM like Azure Key Vault / DigiCert ONE).
   - Issued to: `Ahmad Ibrahim` (or authorized organizational publisher).
2. **Windows SDK Signing Tools:**
   - `signtool.exe` (included with Windows 10/11 SDK, typically located in `C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\`).
3. **Cryptographic Algorithm:**
   - SHA-256 digest algorithm (RFC 3161 compliant timestamping server).

---

## 3. Signing Procedure

### Signing the Executable (`Z EARTH.exe`)
To sign the primary binary with an EV Certificate or PFX key:

```powershell
# Set variables
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
$targetExe = "D:\My Software\Z EARTH\dist\Z EARTH-Preview\Z EARTH.exe"
$timestampUrl = "http://timestamp.digicert.com"

# Execute signing
& $signtool sign /tr $timestampUrl /td sha256 /fd sha256 /d "Z EARTH Desktop GIS" /du "https://github.com/AhmadIbrahim/ZEarth" /a $targetExe
```

### Verifying Signature
To verify the integrity and timestamp of the signed binary:

```powershell
& $signtool verify /pa /v $targetExe
```

Expected output:
```text
Verifying: D:\My Software\Z EARTH\dist\Z EARTH-Preview\Z EARTH.exe
Signature Index: 0 (Primary Signature)
Hash of file (sha256): ...
Signing Certificate Chain:
    ...
The signature is timestamped: ...
Successfully verified: D:\My Software\Z EARTH\dist\Z EARTH-Preview\Z EARTH.exe
Number of files successfully Verified: 1
```

---

## 4. Unsigned Developer Mode Fallback
During local testing and development before an official hardware token certificate is provisioned:
- Windows may display a Defender SmartScreen "Windows protected your PC" banner.
- Users can click **"More info" -> "Run anyway"**.
- The provided PowerShell installer (`scripts/install-zearth.ps1`) runs without requiring elevated code-signing certificates.
