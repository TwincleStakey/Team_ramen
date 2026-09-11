@echo off
rem 대사 테스터 — 프로젝트 루트에서 로컬 서버를 띄우고 브라우저를 연다.
rem (file:// 로 열면 JSON을 못 읽어서 서버가 필요하다. 창을 닫으면 서버도 꺼진다.)
cd /d "%~dp0.."
start "" "http://localhost:8765/Tools/dialogue_tester.html"
python -m http.server 8765 --bind 127.0.0.1
