/// <summary>
/// 死亡函数这块
/// </summary>
    public void Dead()
    {
        // 调用battleField的统一死亡处理函数
        if (battleField != null && IsInstanceValid(battleField))
        {
            battleField.OnUnitDeath(this);
        }
        else
        {
            // 如果battleField不可用，执行基本清理
            if(myPlace!= null)
            {
                myPlace.UnbondCard();
            }
            state = CardState.destroyed;
            this.QueueFree();
        }
    }
