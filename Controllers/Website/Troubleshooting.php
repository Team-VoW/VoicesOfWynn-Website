<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Controllers\Controller;

class Troubleshooting extends WebpageController
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
		self::$data['troubleshooting_title'] = 'Troubleshooting';
		self::$data['troubleshooting_description'] = 'Encountering a problem with the Voices of Wynn mod? Try this flowchart to see if your issue can be resolved quickly and without any further assistance.';
		self::$data['troubleshooting_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Troubleshooting,Flowchart,Support,Help';
		self::$data['troubleshooting_flowchartVersion'] = '1';
		
		self::$views = ['troubleshooting'];
		return true;
	}
}