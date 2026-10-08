#!/bin/bash

# 推送所有分支并删除远程master分支的脚本

echo "======================================"
echo "开始推送分支..."
echo "======================================"

# 推送main分支
echo ""
echo "1. 推送main分支..."
git push -u origin main
if [ $? -eq 0 ]; then
    echo "✓ main分支推送成功"
else
    echo "✗ main分支推送失败"
    exit 1
fi

# 推送dev分支
echo ""
echo "2. 推送dev分支..."
git push -u origin dev
if [ $? -eq 0 ]; then
    echo "✓ dev分支推送成功"
else
    echo "✗ dev分支推送失败"
    exit 1
fi

# 推送upm分支
echo ""
echo "3. 推送upm分支..."
git push -u origin upm
if [ $? -eq 0 ]; then
    echo "✓ upm分支推送成功"
else
    echo "✗ upm分支推送失败"
    exit 1
fi

# 删除远程master分支
echo ""
echo "4. 删除远程master分支..."
git push origin --delete master
if [ $? -eq 0 ]; then
    echo "✓ 远程master分支已删除"
else
    echo "✗ 删除远程master分支失败（可能需要在GitHub设置中更改默认分支）"
fi

echo ""
echo "======================================"
echo "所有操作完成！"
echo "======================================"
echo ""
echo "分支结构："
echo "  - main: 稳定工程版本"
echo "  - dev:  开发分支"
echo "  - upm:  Package发布分支"
echo ""
echo "用户安装命令："
echo "  https://github.com/EitanWong/com.eitan.mnn.git#upm"
echo ""
