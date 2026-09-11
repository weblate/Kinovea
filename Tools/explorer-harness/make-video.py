#!/usr/bin/env python3
# Create a short test video by calling ffmpeg from the command line.
# Assumes ffmpeg is available in PATH.
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

def create_video(resolution, frame_rate, frame_count, gop,output):

    # ffmpeg.exe -f lavfi -i "color=c=black:s=1280x720:r=100" -vf "drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='Frame %{n}\nTime %{pts\:hms}':start_number=0:fontcolor=white:fontsize=72:x=(w-text_w)/2:y=(h-text_h)/2" -frames:v 16 -an -c:v libx264 -pix_fmt yuv420p 16.mp4

    command = [
        "ffmpeg.exe",
        "-f",
        "lavfi",
        "-i",
        f"color=c=black:s={resolution}:r={frame_rate}",
        "-vf",
        "drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='Frame %{n}\nTime %{pts\:hms}':start_number=0:fontcolor=white:fontsize=72:x=(w-text_w)/2:y=(h-text_h)/2",
        "-frames:v",
        frame_count,
        "-an",
        "-c:v",
        "libx264",
        "-g",
        gop,
        "-pix_fmt",
        "yuv420p",
        str(output),
    ]

    try:
        subprocess.run(command, check=True)
    except FileNotFoundError:
        print("Error: ffmpeg was not found in PATH.", file=sys.stderr)
        raise SystemExit(1)
    except subprocess.CalledProcessError as exc:
        print(f"Error: ffmpeg failed with exit code {exc.returncode}.", file=sys.stderr)
        raise SystemExit(exc.returncode)


if __name__ == "__main__":
    resolution = "1280x720"
    frame_rate = "100"
    frame_count = "200"
    gop = "12"
    output = "test.mp4"
    create_video(resolution, frame_rate, frame_count, gop, output)
