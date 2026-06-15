/*




enum InputState
    {
        nil,
        P_InHandCommand,

        P_InHandUnit,
        P_InPlaceUnit,
        P_InHandCommandNeedChooseTarget,
        P_InHandUnitNeedChooseTarget,
        R_InHandCommand,

        R_InHandUnit,
        R_InHandCommandNeedChooseTarget,
        R_InHandUnitNeedChooseTarget,
        P_ChoosingCard,
        R_ChoosingCard,
    }





public override void _Input(InputEvent @event)
    {
        if (ReadControlState() == 1) return;
        
        if (@event is InputEventMouseButton mouseButton)
        {
            // 处理卡牌选择界面的输入
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (isShowingChoiceUI)
                {
                    HandleChoiceCardClick(GetGlobalMousePosition());
                    return; // 选择界面中不要处理其他输入
                }
            }
            
            var mousePosition = GetGlobalMousePosition();
            //选择卡
            if (mouseButton.Pressed)
            {
                
                // 点击事件
                
                var card = CheckCardClick(mousePosition);
                //手上的
                if (card != null && card.getState() == CardState.inHand)
                {
                    // 如果是 Command 卡，改为显示箭头并记录为已选择状态，但不移动卡牌
                    if (card.cardType == CardTypes.Command)
                    {
                        // 只有在点数足够时才允许选择并显示箭头
                        if (player1 != null && player1.HasPoint(card.ReadCost()))
                        {
                            cardNowChoose = card;
                            cardNowChoose.setState(CardState.commandCardCaught);
                            // 显示箭头起点
                            arrowFrom.GlobalPosition = card.GetGlobalPosition() + arrowOffset;
                            cardBase.ProcessMode     = Node.ProcessModeEnum.Inherit;
                            cardBase.Visible         = true;
                            HighlightValidTargets(cardNowChoose.targetType); // 根据 Command 卡的目标类型高亮合法目标
                        }
                        // 点数不足时不做任何反应
                    }
                    else
                    {
                        // 选中时立即重置视觉（避免旋转/缩放导致的瞬移）
                        card.ResetVisualsInstant();

                        offset        = card.GetGlobalPosition() - mousePosition;
                        cardNowChoose = card;
                        card.setState(CardState.caught);
                    }
                }

                //场上的
                if (card != null && card.getState() == CardState.placed)
                {
                    // 检查是否是友方单位，如果是敌方单位则不允许拖动
                    if (card.GetIsFriend() != IsFriend.friend)
                    {
                        return;  // 敌方单位不能被拖动
                    }
                    // 检查是否是总部，总部不能被移动
                    if (card.isHq == HQ.hq)
                    {
                        return;  // 总部不能被移动
                    }
                    
                    // 选中时立即重置视觉（避免旋转/缩放导致的瞬移）
                    card.ResetVisualsInstant();

                    cardNowChoose = card;
                    card.setState(CardState.inplaceAndCaught);

                    //箭头显示
                    arrowFrom.GlobalPosition = card.GetGlobalPosition() + arrowOffset;
                    cardBase.ProcessMode = Node.ProcessModeEnum.Inherit;
                    cardBase.Visible = true;
                    
                    // 高亮合法攻击目标（敌方单位）
                    HighlightValidAttackTargets(card);
                }
                //RefreshAllCardDisplayOrder();
                
                
            }
            //释放时 取消选择
            if (mouseButton.Pressed==false)
            {
                
                //如果现在选着卡
                if(cardNowChoose!= null )
                {
                    

                    //if (validArea.GetGlobalRect().HasPoint(mousePosition))

                    //松开时检查是否在任意一个合法的格子上
                    var result = GetPlaceWithPosition(mousePosition);

                    // 处理 TargetType.NOTarget 的 Command 卡：只要释放点在 validArea 内，直接以 null 目标执行效果
                    if (cardNowChoose.getState() == CardState.commandCardCaught &&
                        cardNowChoose.cardType == CardTypes.Command &&
                        cardNowChoose.targetType == TargetType.NOTarget &&
                        validArea.GetGlobalRect().HasPoint(mousePosition))
                    {
                        if (player1.UsePoint(cardNowChoose.ReadCost()))
                        {
                            _ = ParseAndExecuteEffect(cardNowChoose.effect, cardNowChoose, null);
                            player1.RemoveFromHand(cardNowChoose);
                            RemoveCard(cardNowChoose);
                            CheckIfAnyUnitDiedAsync(); // 结算单位变化
                        }
                        else
                        {
                            // 点数不足，什么也不执行
                        }

                        // 清理并返回，避免后续针对 result 的处理干扰
                        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                        cardBase.Visible     = false;
                        cardNowChoose.setState(CardState.inHand);
                        player1.RefreshMyHand();
                        cardNowChoose = null;
                        RestoreAllTargetsColor();
                        return;
                    }

                    if(result != null)
                    {
                            if(result.GetMyCard()== null)
                            {
                                if(cardNowChoose.getState() == CardState.caught  && player1.UsePoint(cardNowChoose.ReadCost()) == true)
                                {
                                    // 在手上并且有钱的话就移动过去
                                    _ = Move(cardNowChoose,result);
                                }
                                else if(cardNowChoose.getState() == CardState.inplaceAndCaught)
                                {
                                    // 在场上并且拖动到合法格子
                                    _ = Move(cardNowChoose,result);
                                }
                            }
                                else if((result.GetMyCard().GetIsFriend() == IsFriend.enemy ||result.GetMyCard().GetIsFriend() == IsFriend.enemyNeutral)&&cardNowChoose.getState()==CardState.inplaceAndCaught)
                            {
                                //攻击
                                Attack(cardNowChoose,result.GetMyCard());
                            }
                            else if(cardNowChoose.getState() == CardState.commandCardCaught)
                                {
                                    // 如果是 Command 卡，则不移动卡牌，改为生成箭头并记录释放所在格子
                                    if (cardNowChoose.cardType == CardTypes.Command)
                                    {
                                        // 检查玩家是否有足够的点数
                                        if (player1.UsePoint(cardNowChoose.ReadCost()))
                                        {
                                            // 检查目标是否合法
                                            bool isValidTarget = false;
                                            
                                            if (cardNowChoose.targetType == TargetType.NOTarget)
                                            {
                                                // NOTarget 无需指向任何目标，只要在 validArea 内就可以
                                                isValidTarget = validArea.GetGlobalRect().HasPoint(mousePosition);
                                            }
                                            else
                                            {
                                                // 其他目标类型需要检查目标卡是否合法
                                                cardBase_ targetCard = result?.GetMyCard();
                                                isValidTarget = (targetCard != null && IsValidTarget(targetCard, cardNowChoose.targetType)) ||
                                                                (targetCard == null && cardNowChoose.targetType == TargetType.aPlace);
                                            }
                                            
                                            if (isValidTarget)
                                            {
                                                // 目标合法，执行效果
                                                cardBase_ targetCard = result?.GetMyCard();                                                       // 获取目标格子上的卡
                                                          _          = ParseAndExecuteEffect(cardNowChoose.effect, cardNowChoose, [targetCard]);
                                                
                                                // 从手牌中移除这张卡
                                                player1.RemoveFromHand(cardNowChoose);
                                                    // 彻底删除这张卡（会调用Dead()）
                                                RemoveCard(cardNowChoose);
                                                CheckIfAnyUnitDiedAsync(); // 结算单位变化
                                                
                                                // 隐藏箭头
                                                cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                                cardBase.Visible     = false;
                                                cardNowChoose.setState(CardState.inHand);
                                            }
                                            else
                                            {
                                                // 目标不合法，返还点数并恢复状态
                                                player1.RestorePoint(cardNowChoose.ReadCost());
                                                cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                                cardBase.Visible     = false;
                                                cardNowChoose.setState(CardState.inHand);
                                            }
                                        }
                                        else
                                        {
                                            // 点数不足，隐藏箭头并回到手牌状态
                                            cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                            cardBase.Visible     = false;
                                            cardNowChoose.setState(CardState.inHand);
                                        }
                                    }

                                    
                                    
                                }
                            
                            
                        }
                    // 如果没有命中任何 place（result == null），允许对 TargetType.NOTarget 的 Command 卡在 validArea 内释放生效
                    if (result == null && cardNowChoose.getState() == CardState.commandCardCaught)
                    {
                        if (cardNowChoose.cardType == CardTypes.Command)
                        {
                            // 仅当卡的目标类型为 NOTarget 且鼠标在 validArea 内才允许
                            if (cardNowChoose.targetType == TargetType.NOTarget && validArea.GetGlobalRect().HasPoint(mousePosition))
                            {
                                if (player1.UsePoint(cardNowChoose.ReadCost()))
                                {
                                    // 执行效果（无具体目标）
                                    _ = ParseAndExecuteEffect(cardNowChoose.effect, cardNowChoose, null);

                                    // 从手牌中移除并删除该卡
                                    player1.RemoveFromHand(cardNowChoose);
                                    RemoveCard(cardNowChoose);

                                    // 隐藏箭头并恢复状态
                                    cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                    cardBase.Visible     = false;
                                    cardNowChoose.setState(CardState.inHand);
                                }
                                else
                                {
                                    // 点数不足，隐藏箭头并回到手牌状态
                                    cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                    cardBase.Visible     = false;
                                    cardNowChoose.setState(CardState.inHand);
                                }
                            }
                            else
                            {
                                // 非法释放，直接回到手牌状态（没有扣点）
                                cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                cardBase.Visible     = false;
                                cardNowChoose.setState(CardState.inHand);
                            }
                        }
                    }

                    //否则回到起点
                    if (cardNowChoose.getState() == CardState.caught)
                    {
                        cardNowChoose.setState(CardState.inHand);
                    }
                    else if (cardNowChoose.getState() == CardState.inplaceAndCaught)
                    {
                        // 如果是 Command 卡，释放后依然保持在手牌状态（不移动）
                        if (cardNowChoose.cardType == CardTypes.Command)
                        {
                            cardNowChoose.setState(CardState.inHand);
                        }
                        else
                        {
                            cardNowChoose.setState(CardState.placed);
                        }
                    }
                    else if (cardNowChoose.getState() == CardState.commandCardCaught)
                    {
                        // Command 卡如果没有有效释放，恢复为 inHand 状态
                        cardNowChoose.setState(CardState.inHand);
                    }

                    //不管怎样都刷新手牌区
                    player1.RefreshMyHand();
                    cardNowChoose = null;
                    //隐藏箭头

                    cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                    cardBase.Visible     = false;
                    
                    RestoreAllTargetsColor(); // 取消高亮，恢复所有单位的原始颜色
                    CheckIfAnyUnitDiedAsync();
                }
            }
        }
    }

    */