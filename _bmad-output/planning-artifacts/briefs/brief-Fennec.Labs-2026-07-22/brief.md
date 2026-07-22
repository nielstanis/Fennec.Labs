---
title: Fennec.Labs Dashboard
status: draft
created: 2026-07-22
updated: 2026-07-22
---

# Product Brief: Fennec.Labs Dashboard

## Executive Summary

Fennec's CLI commands already produce rich, structured results — OpenSSF scorecards, IL instrumentation traces, assembly diffs — but the only way to consume them today is raw JSON or CLI-rendered tables. That's a good fit for terminal workflows and for LLM/agent consumption (the direction `FennecLabs.Mcp`, FD-013, is already heading), but a poor fit for a developer or security engineer who wants a comprehensive, browsable picture of dependency health.

The Fennec Dashboard turns that existing data into a visual, shareable experience. It runs in two modes on one shared codebase: **project-scoped**, embedded in a .NET project's own context, and **hosted**, a centralized service aggregating the same data across every package a team tracks. v1 ships project-scoped only, with a single view: a project's full transitive dependency tree paired with per-package Scorecard results — reading from a new shared, versioned JSON schema for Fennec result data, rather than today's ad hoc `.fennec/` cache output.

This isn't a new data source — it's making data Fennec already collects actually easy to understand, and setting up the shared data model that lets every other Fennec view (instrumentation, compare/reproduce) and the eventual hosted mode reuse the same foundation instead of being built twice.

## The Problem

Fennec's CLI commands (scorecard, instrumentation, compare/reproduce) already produce rich structured results, but today the only way to consume them is raw JSON or CLI-rendered tables. That's a good fit for terminal workflows and LLM/agent consumption (this is the direction FennecLabs.Mcp is headed), but it's a poor fit for a developer who wants a comprehensive, browsable picture — a project's full transitive dependency tree with per-package scorecards, or an aggregated view across every package a team tracks. There's no visual, shareable surface for that today, whether in a browser or embedded natively in a tool like the GitHub Copilot App.

## The Solution

A Fennec dashboard that renders the results Fennec CLI commands already produce (scorecard, instrumentation, transitive dependency tree, compare/reproduce) as a browsable, visual experience instead of raw JSON. It runs in two modes on the same underlying views: project-scoped, embedded in a .NET project's own context (reading its local `.fennec/` output), and hosted, a centralized service aggregating the same data across every package a team tracks. v1 focuses on the transitive dependency tree + scorecard view. To make both modes work off one codebase, Fennec's result data needs a shared, versioned JSON schema/storage structure that both the local CLI output and the hosted service can read and write.

## What Makes This Different

Not a new data source — Fennec already produces scorecard, instrumentation, and diff data nobody else combines in one tool. The differentiator is reach and usability: one dashboard, built on one shared data model, works whether you're looking at a single project locally or a portfolio of packages hosted centrally — no separate product, no re-implementing views twice. It makes Fennec's existing data actually easy to understand instead of something you have to parse from JSON.

## Who This Serves

Two primary users, across both modes: (1) developers who want to understand and stay on top of their own project's dependency health — transitive tree, scorecards, instrumentation — without leaving their workflow; and (2) application security / platform engineers who need a risk-based view, often across many packages or repos at once, to prioritize what to fix first. Project-scoped serves both at the single-repo scale; hosted serves both at the org/portfolio scale.

## Success Criteria

- Developers reach for the dashboard instead of reading raw JSON when checking a project's dependency health.
- A security engineer can spot a risky transitive dependency (low scorecard score, buried several levels deep) in seconds, not by digging through CLI output.
- The scorecard/dependency-tree view is built once and served, unmodified, by both the project-scoped and hosted modes — no fork, no duplicate implementation.

Concrete usage/adoption metrics (e.g. dashboard opens per week, time-to-find-a-risk) are TBD — worth defining once v1 ships and there's something to measure against.

## Scope

**In for v1:** project-scoped dashboard only, embedded in a .NET project's own context. Views: full transitive dependency tree + per-package Scorecard. Reads from a new shared, versioned JSON schema for Fennec result data (replacing/extending today's `.fennec/` output), with that data optionally committed to the source repo rather than only gitignored cache.

**Out for v1:** the hosted/centralized multi-package service (later, once the shared schema is proven locally), Instrumentation and Compare/Reproduce views (later, using the same shared-view pattern), auth/multi-tenant concerns (irrelevant until hosted exists), and a GitHub Copilot App canvas surface (nice-to-have, not required to prove the concept).

## Vision

Too early to say with confidence. The working hypothesis is that every Fennec CLI feature eventually gets a dashboard view, and hosted mode becomes the org-wide dependency-risk system of record — but that's a hypothesis to validate after v1, not a commitment made here.

