# XO.Diagnostics

[![GitHub Actions Status](https://img.shields.io/github/actions/workflow/status/xo-energy/XO.Diagnostics/ci.yml?branch=main&logo=github)](https://github.com/xo-energy/XO.Diagnostics/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/xo-energy/XO.Diagnostics/branch/main/graph/badge.svg?token=AbiIYmAaeS)](https://codecov.io/gh/xo-energy/XO.Diagnostics)

This repository contains support libraries for integrating OpenTelemetry with the .NET ecosystem and other third-party diagnostics services.

- [XO.Diagnostics.Bugsnag](./XO.Diagnostics.Bugsnag) - a simple HTTP client library for the [BugSnag](https://www.bugsnag.com/) API
- [XO.Diagnostics.Bugsnag.OpenTelemetry](./XO.Diagnostics.Bugsnag.OpenTelemetry) - exports OpenTelemetry traces to Bugsnag
