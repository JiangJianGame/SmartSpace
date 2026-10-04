@echo off
title SmartSpace Colyseus Server
cd /d "%~dp0"
echo Starting SmartSpace Colyseus Server...
npx tsx watch src/index.ts
pause
