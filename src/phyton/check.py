import os
import re
import zipfile
import tarfile
import hashlib
from io import BytesIO
from pathlib import Path
from PIL import Image
import json
from datetime import datetime, timezone
import struct

from pathlib import Path
dt = datetime.now()
print("Data:", dt.strftime("%Y-%m-%d"))