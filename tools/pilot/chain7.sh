#!/usr/bin/env bash
# after the astromancer campaign: the other five classes against the frost and the final boss
cd /y/tools/pilot
until grep -q "campaigns done" camp6.txt 2>/dev/null; do sleep 30; done
ONLY="frost_boss:10 abyss_boss:42" FIRSTNB=-NoBuild bash run_bosses.sh soulbinder wildsoul forgebody lampknight pactbinder > batch3.txt 2>&1
