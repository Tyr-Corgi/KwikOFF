-- Batched image URL update script
-- Updates 100K rows at a time with visible progress

DO $$
DECLARE
    batch_size INT := 100000;
    total_updated INT := 0;
    rows_updated INT;
    min_id BIGINT;
    max_id BIGINT;
    current_min_id BIGINT;
    current_max_id BIGINT;
BEGIN
    -- Get the range of IDs to process
    SELECT MIN(id), MAX(id) INTO min_id, max_id
    FROM open_food_facts_products
    WHERE barcode IS NOT NULL AND LENGTH(barcode) >= 8;
    
    RAISE NOTICE 'Starting batched update from ID % to %', min_id, max_id;
    
    current_min_id := min_id;
    
    -- Process in batches
    WHILE current_min_id <= max_id LOOP
        current_max_id := current_min_id + batch_size - 1;
        
        UPDATE open_food_facts_products
        SET 
            image_url = construct_off_image_url(barcode, '400'),
            image_small_url = construct_off_image_url(barcode, '200'),
            image_front_url = construct_off_image_url(barcode, '400')
        WHERE 
            id >= current_min_id 
            AND id <= current_max_id
            AND barcode IS NOT NULL 
            AND LENGTH(barcode) >= 8;
        
        GET DIAGNOSTICS rows_updated = ROW_COUNT;
        total_updated := total_updated + rows_updated;
        
        -- Commit after each batch
        COMMIT;
        
        RAISE NOTICE 'Batch complete: IDs % to % | Updated: % rows | Total so far: %', 
            current_min_id, current_max_id, rows_updated, total_updated;
        
        current_min_id := current_max_id + 1;
        
        -- Small pause to avoid overwhelming the database
        PERFORM pg_sleep(0.1);
    END LOOP;
    
    RAISE NOTICE 'All batches complete! Total rows updated: %', total_updated;
END $$;

