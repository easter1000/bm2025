import os
import requests
import json

# 1) teams.json에서 팀 목록 로드
with open("teams.json", encoding="utf-8") as f:
    teams = json.load(f)["teams"]

# 2) 저장 폴더 생성
out_dir = "Assets/Resources/team_photos"
os.makedirs(out_dir, exist_ok=True)

# 3) ESPN CDN 기본 URL 패턴
base_url = "https://a.espncdn.com/i/teamlogos/nba/500"

for team in teams:
    abbv = team["team_abbv"].lower()
    default_url = f"{base_url}/{abbv}.png"

    # 시도 1: 기본 abbv
    resp = requests.get(default_url, timeout=10)
    if resp.status_code == 200:
        img_url = default_url
    else:
        # 2차 시도: 팀 이름 기반 URL (소문자, 공백→하이픈)
        name_key = team["team_name"].lower().replace("'", "").replace(" ", "-")
        alt_url = f"{base_url}/{name_key}.png"
        resp = requests.get(alt_url, timeout=10)
        if resp.status_code == 200:
            img_url = alt_url
        else:
            print(f"⚠️ Failed to get logo for {team['team_name']} using {default_url} or {alt_url}")
            continue

    # 파일 저장
    path = os.path.join(out_dir, f"{abbv}.png")
    with open(path, "wb") as img:
        img.write(resp.content)
    print(f"✔ Downloaded {team['team_name']} → {os.path.basename(img_url)}")

print("Done: Assets/Resources/team_photos에 모든 로고가 저장되었습니다.")