#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
generate_players_jersey_map.py

NBA API 에서 모든 현역 선수의 ID, 이름, 등번호를 조회하여
players_jersey_map.json 파일로 저장합니다.

Requirements:
    pip install nba_api pandas
"""

import json
import time
from nba_api.stats.endpoints import commonallplayers, commonplayerinfo
from nba_api.stats.library.parameters import SeasonAll

def fetch_all_players(current_season_only=True):
    """
    모든 선수 목록을 가져옵니다.
    current_season_only=True  → 이번 시즌 로스터 선수
    current_season_only=False → 역대 등록 선수 전체
    """
    df = commonallplayers.CommonAllPlayers(
        is_only_current_season=int(current_season_only)
    ).get_data_frames()[0]
    # PERSON_ID, DISPLAY_FIRST_LAST 컬럼만 사용
    return df[['PERSON_ID', 'DISPLAY_FIRST_LAST']].to_dict('records')

def fetch_player_jersey(player_id):
    """
    단일 선수의 등번호(JERSEY)를 가져옵니다.
    """
    try:
        df = commonplayerinfo.CommonPlayerInfo(player_id=player_id).get_data_frames()[0]
        jersey = df.loc[0, 'JERSEY']
        return str(jersey) if jersey is not None else ""
    except Exception as e:
        # 실패 시 빈 문자열 반환
        print(f"[Warning] player_id={player_id} 조회 실패: {e}")
        return ""

def main():
    # 1) 선수 목록 조회
    players = fetch_all_players(current_season_only=True)

    output = []
    for idx, p in enumerate(players, start=1):
        pid = p['PERSON_ID']
        name = p['DISPLAY_FIRST_LAST']

        # 2) 등번호 조회
        jersey = fetch_player_jersey(pid)
        output.append({
            'player_id': pid,
            'name': name,
            'backnumber': jersey
        })

        # API rate limit 방지 및 예의상 0.6초 대기
        time.sleep(0.6)

        if idx % 50 == 0:
            print(f"Processed {idx}/{len(players)} players...")

    # 3) JSON 파일로 저장
    with open('players_jersey_map.json', 'w', encoding='utf-8') as f:
        json.dump(output, f, ensure_ascii=False, indent=2)

    print(f"✅ 완료: {len(output)}명의 선수 데이터를 players_jersey_map.json 에 저장했습니다.")

if __name__ == '__main__':
    main()
