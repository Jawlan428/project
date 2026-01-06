@echo off
cd /d "C:/Users/user/Documents/GitHub/project/Assets\Videos\Recording_2026-01-06_10-51-18"
"C:/Users/user/Documents/GitHub/project/Assets\ffmpeg-8.0.1-essentials_build\bin\ffmpeg.exe" -y -framerate 30 -i "frame_%%06d.jpg" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p "meeting.mp4"
exit /b %errorlevel%
