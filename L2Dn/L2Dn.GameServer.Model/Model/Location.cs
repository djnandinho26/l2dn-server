﻿using L2Dn.GameServer.Model.Interfaces;
using L2Dn.Geometry;

namespace L2Dn.GameServer.Model;

/**
 * A datatype used to retain a 3D (x/y/z/heading) point. It got the capability to be set and cleaned.
 */
public sealed class Location : ILocational, ILocation3D
{
	private int _x;
	private int _y;
	private int _z;
	private int _heading;
	
	public Location(int x, int y, int z, int heading = 0)
	{
		_x = x;
		_y = y;
		_z = z;
		_heading = heading;
	}
	
	public Location(WorldObject obj): this(obj.getX(), obj.getY(), obj.getZ(), obj.getHeading())
	{
	}

	/**
	 * Get the x coordinate.
	 * @return the x coordinate
	 */
	public int getX()
	{
		return _x;
	}
	
	/**
	 * Get the y coordinate.
	 * @return the y coordinate
	 */
	public int getY()
	{
		return _y;
	}
	
	/**
	 * Get the z coordinate.
	 * @return the z coordinate
	 */
	public int getZ()
	{
		return _z;
	}
	
	/**
	 * Set the x, y, z coordinates.
	 * @param x the x coordinate
	 * @param y the y coordinate
	 * @param z the z coordinate
	 */
	public void setXYZ(int x, int y, int z)
	{
		_x = x;
		_y = y;
		_z = z;
	}
	
	/**
	 * Set the x, y, z coordinates.
	 * @param loc The location.
	 */
	public void setXYZ(ILocational loc)
	{
		setXYZ(loc.getX(), loc.getY(), loc.getZ());
	}
	
	/**
	 * Get the heading.
	 * @return the heading
	 */
	public int getHeading()
	{
		return _heading;
	}
	
	/**
	 * Set the heading.
	 * @param heading the heading
	 */
	public void setHeading(int heading)
	{
		_heading = heading;
	}
	
	public Location getLocation()
	{
		return this;
	}
	
	public void setLocation(Location loc)
	{
		_x = loc.getX();
		_y = loc.getY();
		_z = loc.getZ();
		_heading = loc.getHeading();
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(_x, _y, _z);
	}
	
	public override bool Equals(Object? obj)
	{
		if (obj is Location)
		{
			Location loc = (Location) obj;
			return (getX() == loc.getX()) && (getY() == loc.getY()) && (getZ() == loc.getZ()) && (getHeading() == loc.getHeading());
		}
		return false;
	}
	
	public override string ToString()
	{
		return "[" + GetType().Name + "] X: " + _x + " Y: " + _y + " Z: " + _z + " Heading: " + _heading;
	}

	public Location3D ToLocation3D()
	{
		return new Location3D(_x, _y, _z);
	}

	public int X => _x;
	public int Y => _y;
	public int Z => _z;
}