# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-09-19

First release. Targets Umbraco 18 deliberately, ahead of the v21 LTS, for teams happy to be early.

### Added

- True Copy… tree action that copies a subtree and repoints internal links at the copies.
- Rewriters for Content Picker, Multinode Treepicker, Multi URL Picker, Rich Text and Block List/Grid/Single Block.
- Reports every link still pointing outside the copy.
- ILinkRewriter extension point for custom property editors.
- English and Dutch translations.
