# AmazonApiWorker

Amazon APIを利用して商品データを取得・処理し、
アフィリエイト運用や商品情報管理を自動化するためのC#バックグラウンドツールです。

## What it solves

- Amazonの商品情報取得を自動化
- 商品データの定期取得・更新
- 商品情報の整理・保存
- アフィリエイト運用で必要な商品データ収集の効率化
- 手作業での商品確認・転記作業を削減

## Tech Stack

- C#
- .NET
- Amazon API
- REST API
- JSON
- Background Worker

## Main Features

- Amazon APIとの連携
- 商品情報の取得
- 商品データの加工・整形
- 定期的なバックグラウンド処理
- エラー処理・ログ出力
- 外部データとの連携を想定した構成

## Background

アフィリエイト運用では、
商品名・価格・商品URL・画像・カテゴリなどの情報を
継続的に取得・更新する必要があります。

このツールではAmazon APIを利用し、
商品データの取得から処理までを自動化することで、
運用作業の効率化を目的としています。

## Security

APIキーやSecret Keyなどの認証情報は
リポジトリに含めない設計にしています。

認証情報は環境変数やローカル設定ファイルで管理してください。

## Notes

This repository is published as a portfolio project.
Production credentials and private business data are not included.
