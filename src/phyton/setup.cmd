winget install python
winget install -e --id Gyan.FFmpeg
winget install -e --id Gyan.FFmpeg --silent --accept-package-agreements --accept-source-agreements
python -m pip install --upgrade pip
python -m pip install pillow