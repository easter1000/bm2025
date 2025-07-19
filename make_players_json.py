#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
make_players_json.py
────────────────────────────────────────────────────────
1) nba2k-player-ratings/data/league.json에서 지정 능력치만 추출
2) name으로 nba_api에서 선수 메타(player_id, height, weight, age, position) 매칭
3) 병합하여 Assets/players.json에 저장
"""

import json, math, pathlib, re, time
from datetime import datetime
import pandas as pd
from nba_api.stats.endpoints import commonallplayers, commonteamroster, commonplayerinfo

# ─────────────────────────────────────────────────────
# 1) 경로 설정
# ─────────────────────────────────────────────────────
ROOT     = pathlib.Path(__file__).resolve().parent
ASSETS   = ROOT / "Assets"
OUT_JSON = ASSETS / "players.json"
LEAGUE_JSON = ROOT / "nba2k-player-ratings" / "data" / "league.json"

if not LEAGUE_JSON.exists():
    raise FileNotFoundError(f"{LEAGUE_JSON} 을 찾을 수 없습니다!")

# ─────────────────────────────────────────────────────
# 2) 사용할 컬럼 정의
# ─────────────────────────────────────────────────────
ATTRS = [
    "name", "team", "overallAttribute", "closeShot", "midRangeShot", "threePointShot", "freeThrow",
    "layup", "drivingDunk", "drawFoul", "interiorDefense", "perimeterDefense", "steal", "block",
    "speed", "stamina", "passIQ", "ballHandle", "offensiveRebound", "defensiveRebound"
]

# ─────────────────────────────────────────────────────
# 3) 2K 능력치 데이터 로드
# ─────────────────────────────────────────────────────
print("▶ 2K 능력치 데이터 로드 중…")
with open(LEAGUE_JSON, encoding="utf-8") as f:
    league_data = json.load(f)

# league_data가 리스트인지, 딕셔너리인지 확인
if isinstance(league_data, dict) and "players" in league_data:
    league_players = league_data["players"]
else:
    league_players = league_data

ratings = pd.DataFrame(league_players)[ATTRS]
print(f"  ↳ 2K 선수 수: {len(ratings)}명")

# ─────────────────────────────────────────────────────
# 4) NBA 선수 메타 데이터 수집
# ─────────────────────────────────────────────────────
print("▶ NBA 선수 메타 수집 중…")
cap_df = commonallplayers.CommonAllPlayers(is_only_current_season=1).get_data_frames()[0]
print(f"  ↳ 현역 선수: {len(cap_df)}명")
print("cap_df columns:", cap_df.columns.tolist())

# 팀별 로스터 정보 보강
print("▶ 팀별 로스터 정보 수집 중…")
season  = "2024-25"
roster_map = {}
unmatched_teams = []
team_ids = sorted(cap_df["TEAM_ID"].dropna().unique().astype(int))
for tid in team_ids:
    # 팀 이름 찾기
    team_name = cap_df[cap_df["TEAM_ID"] == tid]["TEAM_NAME"].iloc[0] if not cap_df[cap_df["TEAM_ID"] == tid].empty else str(tid)
    print(f"   • 팀 {team_name} 로스터 불러오는 중…")
    endpoint = None
    try:
        endpoint = commonteamroster.CommonTeamRoster(team_id=tid, season=season)
    except Exception as e:
        print(f"     ! endpoint 생성 실패: {e}")
        unmatched_teams.append(tid)
        continue
    try:
        dfs = endpoint.get_data_frames()
        df_roster = dfs[-1]
    except KeyError as e:
        print(f"     ! Coaches 키 에러 무시: {e}")
        try:
            dfs = endpoint.get_data_frames()
            df_roster = dfs[-1]
        except Exception as e2:
            print(f"     ! 선수 데이터 추출 실패: {e2}")
            continue
    except Exception as e:
        print(f"     ! 선수 데이터 추출 실패: {e}")
        continue
    print(f"팀 {tid} df_roster columns: {df_roster.columns.tolist()}")
    for _, r in df_roster.iterrows():
        pid = int(r["PLAYER_ID"])
        # 각 선수 row의 값이 실제로 None인지 출력
        print(f"  선수: {r.get('PLAYER')} | HEIGHT: {r.get('HEIGHT')} | WEIGHT: {r.get('WEIGHT')} | POSITION: {r.get('POSITION')} | BIRTH_DATE: {r.get('BIRTH_DATE')}")
        roster_map[pid] = {
            "position": r.get("POSITION"),
            "height":   r.get("HEIGHT"),
            "weight":   r.get("WEIGHT"),
            "birth":    r.get("BIRTH_DATE")
        }
    time.sleep(1)

# 이름 정규화 함수 (강화)
import unicodedata

def norm(s):
    if not isinstance(s, str):
        return ""
    s = s.lower()
    s = unicodedata.normalize('NFKD', s)
    s = re.sub(r"[^a-z0-9]", "", s)
    s = re.sub(r"(jr|sr|iii|ii|iv)$", "", s)
    return s

# cap_df에 key 컬럼 추가
cap_df["key"] = cap_df["DISPLAY_FIRST_LAST"].apply(norm)
# roster_map을 player_id 기준으로 메타 dict로 변환
meta_map = {}
for _, r in cap_df.iterrows():
    pid = int(r["PERSON_ID"])
    meta = roster_map.get(pid, {})
    meta_map[norm(r["DISPLAY_FIRST_LAST"])] = {
        "player_id": pid,
        "height": meta.get("height"),
        "weight": meta.get("weight"),
        "age": None,
        "position": meta.get("position")
    }
    # 나이 계산
    birth = meta.get("birth")
    if birth:
        try:
            d = datetime.strptime(str(birth).split("T")[0], "%Y-%m-%d")
            meta_map[norm(r["DISPLAY_FIRST_LAST"])] ["age"] = math.floor((datetime.now() - d).days / 365.25)
        except:
            pass

# 포지션 매핑
POSITION_MAP = {
    'PG': 1, 'SG': 2, 'SF': 3, 'PF': 4, 'C': 5,
    'GUARD': 1, 'GUARD-FORWARD': 1, 'FORWARD-GUARD': 3,
    'FORWARD': 3, 'FORWARD-CENTER': 4, 'CENTER-FORWARD': 5, 'CENTER': 5
}

def parse_height(h):
    if not isinstance(h, str):
        return None
    h = h.strip().replace('"', '').replace("'", "").replace(' ', '-')
    m = re.match(r"(\d+)[- ]?(\d+)", h)
    if m:
        return f"{m.group(1)}-{m.group(2)}"
    m = re.match(r"(\d+)[- ]?", h)
    if m:
        return f"{m.group(1)}-0"
    return None

def parse_position(pos):
    if not isinstance(pos, str):
        return None
    pos = pos.upper().strip()
    # 복합 포지션은 '-' 또는 '/'로 구분, 첫 번째만 사용
    pos = re.split(r'[-/]', pos)[0]
    return POSITION_MAP.get(pos, None)

def get_playerinfo(player_id):
    try:
        info = commonplayerinfo.CommonPlayerInfo(player_id=player_id)
        df = info.get_data_frames()[0]
        height = parse_height(df["HEIGHT"].iloc[0])
        try:
            weight = int(df["WEIGHT"].iloc[0])
        except:
            weight = None
        birth = df["BIRTHDATE"].iloc[0]
        age = None
        if isinstance(birth, str) and len(birth) >= 4:
            try:
                birth_year = int(birth[:4])
                age = 2024 - birth_year
            except:
                age = None
        position = parse_position(df["POSITION"].iloc[0])
        return {
            "height": height,
            "weight": weight,
            "age": age,
            "position": position
        }
    except Exception as e:
        print(f"  ! player_id {player_id} info 실패: {e}")
        return {"height": None, "weight": None, "age": None, "position": None}

# ─────────────────────────────────────────────────────
# 5) 병합: name 기준으로 메타+능력치 합치기
# ─────────────────────────────────────────────────────
print("▶ 병합 및 JSON 저장 중…")
rows = []
unmatched = []
for _, r in ratings.iterrows():
    key = norm(r["name"])
    meta = meta_map.get(key, {})
    pid = meta.get("player_id")
    # commonplayerinfo로 height, weight, age, position 가져오기
    height = weight = age = position = None
    if pid is not None:
        info = get_playerinfo(pid)
        height = info["height"]
        weight = info["weight"]
        age = info["age"]
        position = info["position"]
        time.sleep(0.6)  # API rate limit 우회
    if not meta or meta.get("player_id") is None:
        unmatched.append(r["name"])
    row = {
        "player_id": pid,
        "name": r["name"],
        "team": r["team"],
        "height": height,
        "weight": weight,
        "age": age,
        "position": position,
    }
    for attr in ATTRS:
        if attr not in row:
            row[attr] = r[attr]
    rows.append(row)

if unmatched:
    print("매칭 실패 선수 (player_id 등 메타가 null):")
    for name in unmatched:
        print("  -", name)

ASSETS.mkdir(exist_ok=True, parents=True)
with open(OUT_JSON, "w", encoding="utf-8") as f:
    json.dump({"players": rows}, f, ensure_ascii=False, indent=2)

print(f"✅ 완료! 총 {len(rows)}명 → {OUT_JSON.relative_to(ROOT)}")
