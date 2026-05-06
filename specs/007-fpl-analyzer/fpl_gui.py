#!/usr/bin/env python3
"""Streamlit interface for the FPL Ultimate Analyzer V11.0
(Squad-Continuity + Multi-Week Transfer Planner)."""

import logging
import sys

import pandas as pd
import plotly.express as px
import streamlit as st


# ─────────────────────────────────────────────────────────────────────────────
# Logging: route the FPL logger to the terminal Streamlit was launched from.
# Streamlit reruns this script on every interaction, so we guard against adding
# duplicate handlers each time.
# ─────────────────────────────────────────────────────────────────────────────
def _setup_logging():
    fpl_log = logging.getLogger('FPL')
    if not any(getattr(h, '_fpl_gui_handler', False) for h in fpl_log.handlers):
        handler = logging.StreamHandler(sys.stderr)
        handler.setFormatter(logging.Formatter(
            '%(asctime)s [%(levelname)s] %(message)s', datefmt='%H:%M:%S'
        ))
        handler._fpl_gui_handler = True  # marker so reruns don't duplicate
        fpl_log.addHandler(handler)
    fpl_log.setLevel(logging.INFO)
    fpl_log.propagate = False  # don't double-print via root logger


_setup_logging()

from fpl import run_analysis

st.set_page_config(page_title='FPL Ultimate Analyzer', page_icon='⚽', layout='wide')


# ─────────────────────────────────────────────────────────────────────────────
# Caching: don't refit + refetch on every toggle change.
# Keys on (gw, horizon, budget, team_id, no_understat). Display toggles in the
# sidebar do NOT invalidate this cache because they don't affect the analysis.
# ─────────────────────────────────────────────────────────────────────────────
@st.cache_data(ttl=3600, show_spinner=False, max_entries=8)
def cached_run_analysis(target_gw, budget, horizon, team_id, no_understat):
    return run_analysis(
        target_gw=target_gw,
        budget=budget,
        horizon=horizon,
        team_id=team_id,
        no_understat=no_understat,
    )


def _format_money(v):
    return f"£{float(v):.1f}m"


def _decorate_squad_table(df, captain_name=None, vice_name=None):
    cols = [c for c in ['player_name', 'team_name', 'position', 'price',
                        'predicted', 'horizon_total', 'selected_by_percent', 'fixture_ticker_5']
            if c in df.columns]
    view = df[cols].copy()
    rename_map = {
        'player_name': 'Player', 'team_name': 'Team', 'position': 'Pos',
        'price': 'Price', 'predicted': 'GW Pred', 'horizon_total': 'Weighted Total',
        'selected_by_percent': 'Own %', 'fixture_ticker_5': 'Fixture Ticker',
    }
    view = view.rename(columns=rename_map)
    if captain_name is not None and 'Player' in view.columns:
        roles = ['Captain' if p == captain_name else ('Vice' if p == vice_name else '') for p in view['Player']]
        view.insert(0, 'Role', roles)
    return view


# ─────────────────────────────────────────────────────────────────────────────
# Section renderers
# ─────────────────────────────────────────────────────────────────────────────

def _render_continuity_header(plan, gw, horizon):
    cols = st.columns(5)
    cols[0].metric('Bank', _format_money(plan['bank']))
    cols[1].metric('Free Transfers', f"{plan['free_transfers']}")
    cols[2].metric('Captain', f"{plan['captain_name']} ({plan['captain_team']})")
    cols[3].metric(f'GW{gw} XI Pred', f"{plan['current_score_gw1']:.2f}")
    cols[4].metric(f'{horizon}-GW Score', f"{plan['current_score_horizon']:.2f}")


def _render_recommendation_card(plan):
    rec = plan.get('recommended')
    st.subheader('Recommendation for this Gameweek')
    if rec is None:
        st.info('No recommendation could be computed.')
        return

    kind = rec.get('kind', 'hold')
    if kind == 'hold':
        st.success("**HOLD** — no positive-gain transfer was found.")
        if rec.get('note'):
            st.caption(rec['note'])
        return

    gain = rec['gain']
    color = st.success if gain > 4 else (st.info if gain > 0 else st.warning)
    color(f"**{kind.upper().replace('_', ' ')}** — Net gain after hits: **{gain:+.2f}** | Hit cost: **{rec['hit_cost']}**")

    if rec.get('transfers'):
        rows = []
        for t in rec['transfers']:
            rows.append({
                'Out': f"{t['out_name']} ({t['out_team']})",
                'Out Horizon': f"{t['out_horizon_total']:.2f}",
                'In': f"{t['in_name']} ({t['in_team']})",
                'In Horizon': f"{t['in_horizon_total']:.2f}",
                'Δ': f"{t['delta']:+.2f}",
            })
        st.table(pd.DataFrame(rows))

    cols = st.columns(2)
    cols[0].metric('New Bank', _format_money(rec['new_bank']))
    cols[1].metric('New Horizon Score', f"{rec['new_score']:.2f}")


def _render_alternatives(plan):
    alts = plan.get('alternatives', [])
    rec = plan.get('recommended')
    others = [p for p in alts if p is not rec]
    if not others:
        return
    with st.expander(f'Other single-week options considered ({len(others)})'):
        for i, p in enumerate(others[:5], 1):
            kind = p.get('kind', '?')
            if kind == 'hold':
                st.write(f"**{i}. HOLD** — gain: {p['gain']:+.2f}")
            else:
                summary = ', '.join(f"{t['out_name']} → {t['in_name']}" for t in p['transfers'])
                st.write(f"**{i}. {kind.upper().replace('_',' ')}** — {summary}  "
                         f"(gain: {p['gain']:+.2f}, hit: {p['hit_cost']})")


def _render_squad_tables(plan):
    captain_name = plan.get('captain_name')
    vice_name = plan.get('vice_captain_name')
    xi_view = _decorate_squad_table(
        plan['xi_df'].sort_values(['position_id', 'horizon_total'], ascending=[True, False]),
        captain_name=captain_name, vice_name=vice_name,
    )
    bench_view = _decorate_squad_table(
        plan['bench_df'].sort_values('predicted', ascending=False),
        captain_name=captain_name, vice_name=vice_name,
    )
    left, right = st.columns([1.4, 1.0])
    with left:
        st.subheader('Your Starting XI')
        st.dataframe(xi_view, use_container_width=True, hide_index=True)
    with right:
        st.subheader('Your Bench')
        st.dataframe(bench_view, use_container_width=True, hide_index=True)


def _render_outlook(outlook_df, gw, horizon, title='Multi-GW Outlook'):
    if outlook_df is None or len(outlook_df) == 0:
        return
    st.subheader(title)
    display_cols = ['player_name', 'team_name', 'position', 'price']
    for gw_num in range(gw, gw + horizon):
        if f'pred_gw{gw_num}' in outlook_df.columns:
            display_cols.append(f'pred_gw{gw_num}')
    display_cols += [c for c in ['horizon_total', 'fixture_ticker_5'] if c in outlook_df.columns]
    view = outlook_df[display_cols].copy()
    rename_map = {'player_name': 'Player', 'team_name': 'Team', 'position': 'Pos',
                  'price': 'Price', 'horizon_total': 'Weighted Total', 'fixture_ticker_5': 'Fixture Ticker'}
    for gw_num in range(gw, gw + horizon):
        rename_map[f'pred_gw{gw_num}'] = f'GW{gw_num}'
    st.dataframe(view.rename(columns=rename_map), use_container_width=True, hide_index=True)

    chart_col, heatmap_col = st.columns(2)
    with chart_col:
        bar_df = outlook_df[['player_name', 'horizon_total', 'position']].copy().sort_values('horizon_total', ascending=False)
        fig = px.bar(bar_df, x='player_name', y='horizon_total', color='position',
                     title='Weighted Horizon Total by Player')
        fig.update_layout(xaxis_title='', yaxis_title='Weighted Points', legend_title='Position')
        st.plotly_chart(fig, use_container_width=True)
    with heatmap_col:
        gw_cols = [f'pred_gw{gw_num}' for gw_num in range(gw, gw + horizon) if f'pred_gw{gw_num}' in outlook_df.columns]
        if gw_cols:
            heatmap_df = outlook_df[['player_name'] + gw_cols].copy().set_index('player_name')
            fig = px.imshow(heatmap_df, aspect='auto', color_continuous_scale='YlGnBu',
                            title='Predicted Points Across the Planning Horizon',
                            labels={'color': 'Predicted Pts'})
            fig.update_layout(xaxis_title='Gameweek', yaxis_title='Player')
            st.plotly_chart(fig, use_container_width=True)


def _render_chip_cards(chip_plan, show_tc=True, show_bb=True, show_fh=True, show_wc=True):
    enabled = [k for k, v in [('tc', show_tc), ('bb', show_bb), ('fh', show_fh), ('wc', show_wc)] if v]
    if not enabled:
        return
    cols = st.columns(len(enabled))
    idx = 0
    if show_tc:
        tc = chip_plan.get('triple_captain')
        text = 'No recommendation available'
        if tc:
            text = f"GW{tc['gw']} — {tc['player_name']} ({tc['team_name']})\n\nCeiling: {tc['ceiling']:.2f}"
        cols[idx].info(f"**Triple Captain**\n\n{text}")
        idx += 1
    if show_bb:
        bb = chip_plan.get('bench_boost')
        text = 'No recommendation available'
        if bb:
            text = f"GW{bb['gw']}\n\nBench projection: {bb['bench_points']:.2f}\n\n{', '.join(bb.get('bench_names', []))}"
        cols[idx].info(f"**Bench Boost**\n\n{text}")
        idx += 1
    if show_fh:
        fh = chip_plan.get('free_hit')
        text = 'No recommendation available'
        if fh:
            text = f"GW{fh['gw']}\n\nSwing score: {fh['swing_score']:.2f}\n\nBlanks: {fh['blank_teams']} | Doubles: {fh['double_teams']}"
        cols[idx].info(f"**Free Hit**\n\n{text}")
        idx += 1
    if show_wc:
        wc = chip_plan.get('wildcard')
        text = 'No recommendation available'
        if wc:
            gw_text = f"GW{wc['gw']}" if wc.get('gw') else 'Hold'
            text = f"{gw_text}\n\n{wc['reason']}"
        cols[idx].info(f"**Wildcard**\n\n{text}")
        idx += 1


# ─────────────────────────────────────────────────────────────────────────────
# V11 NEW: Multi-week plan renderer
# ─────────────────────────────────────────────────────────────────────────────

def _render_multi_week_plan(mw_plan):
    if mw_plan is None:
        return
    st.subheader('🗓️ Multi-Week Transfer Plan')
    best = mw_plan['best_path']

    cols = st.columns(4)
    cols[0].metric('Plan Total Score', f"{mw_plan['best_total_score_with_transfers']:.2f}")
    cols[1].metric('Hold Baseline', f"{mw_plan['baseline_horizon_total']:.2f}")
    cols[2].metric('Net Gain vs Baseline', f"{mw_plan['best_total_net_gain_vs_baseline']:+.2f}")
    cols[3].metric('Total Hits', f"{best['cumulative_hits']}")

    st.caption('Beam search across the planning horizon. Each row = one gameweek decision. '
               'Bank and free transfers carry over (FTs cap at 5).')

    rows = []
    for step in best['path']:
        if step['transfer_count'] == 0:
            rows.append({
                'GW': step['gw'],
                'Action': 'HOLD',
                'Transfers': '—',
                'Hit': step['hit_cost'],
                'Week Pred': f"{step['week_score']:.2f}",
                'Bank After': _format_money(step['new_bank']),
                'FTs After': step['fts_after'],
            })
        else:
            summary = ', '.join(f"{t['out_name']} → {t['in_name']}" for t in step['transfers'])
            rows.append({
                'GW': step['gw'],
                'Action': f"{step['transfer_count']}-transfer",
                'Transfers': summary,
                'Hit': step['hit_cost'],
                'Week Pred': f"{step['week_score']:.2f}",
                'Bank After': _format_money(step['new_bank']),
                'FTs After': step['fts_after'],
            })
    st.dataframe(pd.DataFrame(rows), use_container_width=True, hide_index=True)

    if mw_plan.get('alternatives'):
        with st.expander(f"Alternative paths ({len(mw_plan['alternatives'])})"):
            alt_rows = []
            for i, alt in enumerate(mw_plan['alternatives'][:3], 1):
                summary = ' | '.join(
                    'HOLD' if s['transfer_count'] == 0 else f"{s['transfer_count']}T@GW{s['gw']}"
                    for s in alt['path']
                )
                alt_rows.append({
                    'Path': i,
                    'Steps': summary,
                    'Cumulative Gain': f"{alt['cumulative_gain']:+.2f}",
                    'Total Hits': alt['cumulative_hits'],
                })
            st.dataframe(pd.DataFrame(alt_rows), use_container_width=True, hide_index=True)


def _render_from_scratch_view(result, show_differentials, show_tc, show_bb, show_fh, show_wc):
    pred_df = result['pred_df']
    squad = result['squad']

    c1, c2, c3, c4 = st.columns(4)
    c1.metric('Budget Used', _format_money(result['budget']))
    c2.metric('Captain', pred_df.loc[squad['captain'], 'player_name'])
    c3.metric(f"GW{result['gw']} Pred XI", f"{squad.get('xi_pred_gw1', 0):.2f}")
    c4.metric('Optimizer Score', f"{squad.get('xi_pred_score', 0):.2f}")

    xi_df = pred_df.loc[squad['xi']].sort_values(['position_id', 'horizon_total'], ascending=[True, False])
    bench_df = pred_df.loc[squad['bench']].sort_values('predicted', ascending=False)

    captain_name = pred_df.loc[squad['captain'], 'player_name']
    vice_name = pred_df.loc[squad['vice_captain'], 'player_name']

    left, right = st.columns([1.4, 1.0])
    with left:
        st.subheader('Optimal Starting XI')
        st.dataframe(_decorate_squad_table(xi_df, captain_name, vice_name),
                     use_container_width=True, hide_index=True)
    with right:
        st.subheader('Optimal Bench')
        st.dataframe(_decorate_squad_table(bench_df, captain_name, vice_name),
                     use_container_width=True, hide_index=True)

    _render_outlook(result['multi_gw_outlook'], result['gw'], result['horizon'],
                    title='From-Scratch Multi-GW Outlook')

    if show_differentials:
        st.subheader('Differential Picks')
        diff_df = result.get('differential_df')
        if diff_df is not None and len(diff_df) > 0:
            st.dataframe(_decorate_squad_table(diff_df), use_container_width=True, hide_index=True)
        else:
            st.info('No low-ownership differential table was generated for this run.')

    st.subheader('Chip Strategy')
    _render_chip_cards(result.get('chip_plan', {}), show_tc=show_tc, show_bb=show_bb,
                       show_fh=show_fh, show_wc=show_wc)


# ─────────────────────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────────────────────

def main():
    st.title('Fantasy Premier League Ultimate Analyzer')
    st.caption('V11 — Squad continuity + multi-week transfer planner. '
               'Quantile-regression ceilings + Poisson XGBoost in the ensemble.')

    with st.sidebar:
        st.header('Planner Settings')
        gw = st.number_input('Target GW (0 = auto-detect)', min_value=0, max_value=60, value=0, step=1)
        horizon = st.slider('Planning horizon', min_value=1, max_value=5, value=3, step=1)
        budget = st.slider('Budget (£m, used only when no Team ID)', min_value=80.0, max_value=110.0, value=100.0, step=0.1)
        team_id_text = st.text_input('FPL Team ID', value='', help='Find your ID in the FPL "Points" page URL.')
        no_understat = st.checkbox('Skip Understat data', value=False)

        st.subheader('View Toggles')
        show_multi_week = st.toggle('Show multi-week transfer plan', value=True,
                                     help='Beam search across the horizon, with FT/bank rollover.')
        show_from_scratch = st.toggle('Show from-scratch reference squad', value=False,
                                       help='Useful as a Wildcard preview when you have a Team ID.')
        show_differentials = st.toggle('Show differential picks', value=True)

        st.subheader('Chip Cards')
        show_tc = st.toggle('Triple Captain', value=True)
        show_bb = st.toggle('Bench Boost', value=True)
        show_fh = st.toggle('Free Hit', value=True)
        show_wc = st.toggle('Wildcard', value=True)

        run = st.button('Run Analysis', type='primary', use_container_width=True)
        if st.button('Clear cached run', use_container_width=True):
            cached_run_analysis.clear()
            st.success('Cached results cleared. Click Run Analysis again.')

    if not run:
        st.info('Enter your **FPL Team ID** in the sidebar to get squad-continuity mode '
                '(transfers / hold / captain advice on the team you actually own). '
                'Without a Team ID the planner falls back to from-scratch optimization.')
        return

    team_id = int(team_id_text) if str(team_id_text).strip().isdigit() else None
    target_gw = None if gw == 0 else int(gw)

    with st.spinner('Fetching data, training the V11 ensemble, building the planner... (cached for 1 hour)'):
        result = cached_run_analysis(
            target_gw=target_gw,
            budget=budget,
            horizon=horizon,
            team_id=team_id,
            no_understat=no_understat,
        )

    if not result or result.get('mode') != 'analysis':
        st.error('The analysis did not return a valid planning result.')
        return

    primary_view = result.get('primary_view', 'from_scratch')
    plan = result.get('current_squad_plan')
    mw_plan = result.get('multi_week_plan')

    if primary_view == 'continuity' and plan is not None:
        st.success(f"Squad-continuity mode for Team {result.get('team_id')} — "
                    f"GW{result['gw']} across a {result['horizon']}-GW horizon.")
        st.caption('⚠️ Public FPL endpoints only update *after each deadline*. If you have already '
                   'made transfers since the latest finished GW, those are not reflected here.')
        _render_continuity_header(plan, result['gw'], result['horizon'])
        _render_recommendation_card(plan)
        _render_alternatives(plan)
        _render_squad_tables(plan)

        # V11: Multi-week plan goes here, between the "this week" block and the outlook.
        if show_multi_week and mw_plan is not None:
            _render_multi_week_plan(mw_plan)

        _render_outlook(plan['outlook_df'], result['gw'], result['horizon'],
                        title='Your Squad — Multi-GW Outlook')

        if show_differentials:
            st.subheader('Differential Picks (low-ownership candidates)')
            diff_df = result.get('differential_df')
            if diff_df is not None and len(diff_df) > 0:
                st.dataframe(_decorate_squad_table(diff_df), use_container_width=True, hide_index=True)
            else:
                st.info('No differential table generated for this run.')

        st.subheader('Chip Strategy')
        _render_chip_cards(result.get('chip_plan', {}), show_tc, show_bb, show_fh, show_wc)

        if show_from_scratch:
            with st.expander('🔄 Reference: from-scratch optimum (Wildcard preview)'):
                _render_from_scratch_view(result, show_differentials=False,
                                           show_tc=False, show_bb=False, show_fh=False, show_wc=False)
    else:
        st.success(f"From-scratch optimization for GW{result['gw']} across "
                    f"a {result['horizon']}-GW horizon. Add a Team ID for continuity mode.")
        _render_from_scratch_view(result, show_differentials, show_tc, show_bb, show_fh, show_wc)

    with st.expander('Model Diagnostics (V11 ensemble: + Poisson + Quantile q=0.9)'):
        top_feats = pd.DataFrame(result.get('top_features', []), columns=['Feature', 'Importance'])
        if len(top_feats) > 0:
            st.dataframe(top_feats, use_container_width=True, hide_index=True)
        st.write(f"Baseline MAE: **{result.get('baseline_mae')}**")
        st.json(result.get('model_weights', {}))


if __name__ == '__main__':
    main()
