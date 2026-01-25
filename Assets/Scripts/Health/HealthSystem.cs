public class HealthSystem
{
    private int health;

    public HealthSystem(int initialHealth)
    {
        health = initialHealth;
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health < 0) health = 0;
    }

    public int GetHealth()
    {
        return health;
    }

    public bool IsAlive()
    {
        return health > 0;
    }
}